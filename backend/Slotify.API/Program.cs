using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;
using Scalar.AspNetCore;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Notifications;
using Slotify.Infrastructure.Repositories;
using Slotify.Infrastructure.Security;
using Slotify.API;

var builder = WebApplication.CreateBuilder(args);

// --- Persistencia ---
builder.Services.AddDbContext<SlotifyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Configuración JWT ---
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");
builder.Services.AddSingleton(jwtOptions);

// --- Configuración de cifrado (ADR #5) ---
var cryptoOptions = builder.Configuration.GetSection("Crypto").Get<CryptoOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Crypto'.");
builder.Services.AddSingleton(cryptoOptions);
builder.Services.AddScoped<ICryptoService, AesGcmCryptoService>();
builder.Services.AddScoped<IBlindIndex, HmacBlindIndex>();

// --- Repositorios y servicios (Repository Pattern + DI, ADR #2) ---
builder.Services.AddScoped<IBusinessRepository, BusinessRepository>();
builder.Services.AddScoped<ITierRepository, TierRepository>();
builder.Services.AddScoped<IStaffRepository, StaffRepository>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<IStaffServiceRepository, StaffServiceRepository>();
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationSender, LoggedNotificationSender>();
builder.Services.AddScoped<IBusinessHourRepository, BusinessHourRepository>();
builder.Services.AddScoped<IBusinessHolidayRepository, BusinessHolidayRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<IPasswordResetEmailSender, LoggedPasswordResetEmailSender>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IFreemiumLimitService, FreemiumLimitService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<ServiceService>();
builder.Services.AddScoped<StaffService>();
builder.Services.AddScoped<StaffServiceAssignmentService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ReservationManagementService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<ReminderBackgroundService>();
builder.Services.AddScoped<GuestReservationLookupService>();
builder.Services.AddScoped<BusinessScheduleService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<PasswordResetService>();

// --- URL pública del frontend (para los enlaces de los emails simulados) ---
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Frontend").Get<FrontendOptions>() ?? new FrontendOptions());

// --- Autenticación JWT (ADR #3) ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conserva los nombres de claim del JWT (sub, email)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
        };
    });
builder.Services.AddAuthorization();

// --- CORS (frontend React/Vite) ---
const string FrontendCorsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// --- Rate limiting (anti fuerza bruta en auth) ---
// Sin limitador global: los e2e y el resto de la API no se ven afectados. La política
// "auth" (fixed window por IP) se aplica solo a login/register vía [EnableRateLimiting].
// Los límites se leen vía IOptions en tiempo de petición (lazy) para que los tests de
// integración puedan sobreescribirlos desde la WebApplicationFactory.
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var rateLimiting = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var retryAfterSeconds = rateLimiting.AuthWindowSeconds;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);

        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "rate_limited", message = "Demasiadas peticiones. Inténtalo de nuevo más tarde." }, ct);
    };
    options.AddPolicy("auth", httpContext =>
    {
        var rateLimiting = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimiting.AuthPermitLimit,
                Window = TimeSpan.FromSeconds(rateLimiting.AuthWindowSeconds),
                QueueLimit = 0,
            });
    });
});

// --- API / OpenAPI ---
builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<Slotify.API.OpenApi.BearerSecuritySchemeTransformer>());

var app = builder.Build();

// --- Migraciones al arranque ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                  // /openapi/v1.json
    app.MapScalarApiReference();                       // UI interactiva en /scalar
}

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expone Program para los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
