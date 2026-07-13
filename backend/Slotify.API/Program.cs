using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;
using Scalar.AspNetCore;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Notifications;
using Slotify.Infrastructure.Repositories;
using Slotify.Infrastructure.Security;
using Slotify.API;
using Slotify.API.Realtime;

var builder = WebApplication.CreateBuilder(args);

// --- Logging estructurado (Serilog) ---
// Niveles desde la sección "Serilog" de appsettings (Microsoft.AspNetCore → Warning).
// Sink de consola según entorno: JSON compacto (CLEF) en Production para que Docker
// recoja stdout estructurado; texto plano legible en Development/tests.
// Privacidad: nunca loguear bodies ni query strings (datos personales cifrados en BD).
builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration.ReadFrom.Configuration(context.Configuration);
    if (context.HostingEnvironment.IsProduction())
        loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
    else
        loggerConfiguration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
});

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
builder.Services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
builder.Services.AddScoped<IAccountEmailSender, LoggedAccountEmailSender>();
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
builder.Services.AddScoped<EmailVerificationService>();
builder.Services.AddScoped<IAccountDeletionRepository, AccountDeletionRepository>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddScoped<IBusinessDeletionRepository, BusinessDeletionRepository>();
builder.Services.AddScoped<BusinessDeletionService>();

// --- Admin de plataforma (moderación): email configurado en Admin:Email / Admin__Email ---
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Admin").Get<AdminOptions>() ?? new AdminOptions());
builder.Services.AddScoped<ISupportEmailSender, LoggedSupportEmailSender>();
builder.Services.AddScoped<SupportService>();

// --- Formulario de contacto/soporte: destinatario del email simulado ---
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Support").Get<SupportOptions>() ?? new SupportOptions());

// --- URL pública del frontend (para los enlaces de los emails simulados) ---
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Frontend").Get<FrontendOptions>() ?? new FrontendOptions());

// --- Email real por SMTP (IONOS, STARTTLS) si hay credenciales en el entorno
// (SMTP_HOST/SMTP_PORT/SMTP_USER/SMTP_PASSWORD); si faltan (desarrollo local),
// quedan los senders simulados registrados arriba. Los avisos de WhatsApp no
// tienen proveedor real y siguen simulados en ambos modos. ---
var smtpOptions = SmtpOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddScoped<LoggedNotificationSender>();
if (smtpOptions.IsConfigured)
{
    builder.Services.AddScoped<ISmtpTransport, MailKitSmtpTransport>();
    builder.Services.AddScoped<SmtpEmailSender>();
    builder.Services.AddScoped<IAccountEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
    builder.Services.AddScoped<INotificationSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
    builder.Services.AddScoped<ISupportEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
}

// --- WhatsApp real vía Twilio si hay credenciales en el entorno
// (TWILIO_ACCOUNT_SID/TWILIO_AUTH_TOKEN/TWILIO_WHATSAPP_FROM); el wrapper manda el
// canal 'whatsapp' por Twilio y delega el resto en la cadena de email de arriba
// (SMTP real o simulado). Sin credenciales, WhatsApp sigue simulado por log. ---
var twilioOptions = TwilioOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(twilioOptions);
if (twilioOptions.IsConfigured)
{
    builder.Services.AddHttpClient<TwilioWhatsAppTransport>();
    builder.Services.AddScoped<IWhatsAppTransport>(sp => sp.GetRequiredService<TwilioWhatsAppTransport>());
    builder.Services.AddScoped<INotificationSender>(sp => new WhatsAppNotificationSender(
        sp.GetRequiredService<IWhatsAppTransport>(),
        smtpOptions.IsConfigured
            ? sp.GetRequiredService<SmtpEmailSender>()
            : sp.GetRequiredService<LoggedNotificationSender>(),
        sp.GetRequiredService<ILogger<WhatsAppNotificationSender>>()));
}

// --- OTP de invitado (verificar identidad antes del lookup/cancelar/reprogramar):
// email real si hay SMTP, teléfono por WhatsApp si hay Twilio; si no, simulados. ---
builder.Services.AddScoped<IGuestOtpRepository, GuestOtpRepository>();
builder.Services.AddScoped<GuestOtpService>();
builder.Services.AddScoped<IGuestOtpSender>(sp => new GuestOtpSender(
    smtpOptions.IsConfigured ? sp.GetRequiredService<ISmtpTransport>() : null,
    twilioOptions.IsConfigured ? sp.GetRequiredService<IWhatsAppTransport>() : null,
    sp.GetRequiredService<SmtpOptions>(),
    sp.GetRequiredService<ILogger<GuestOtpSender>>()));

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
        // SignalR no puede mandar cabeceras en WebSockets: el token viaja como
        // ?access_token= SOLO en las rutas de hubs (convención de ASP.NET Core).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
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

// --- Health checks (observabilidad) ---
// "database" (tag ready) verifica conectividad real con PostgreSQL vía el DbContext.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SlotifyDbContext>("database", tags: ["ready"]);

// --- API / OpenAPI ---
builder.Services.AddControllers();

// --- Tiempo real (SignalR): eventos "reservationChanged" a cliente y negocio ---
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<Slotify.API.OpenApi.BearerSecuritySchemeTransformer>());

var app = builder.Build();

if (smtpOptions.IsConfigured)
{
    app.Logger.LogInformation(
        "Emails por SMTP real: {Host}:{Port} (remitente {From})",
        smtpOptions.Host, smtpOptions.Port, smtpOptions.FromAddress);
}
else
{
    app.Logger.LogInformation("Emails simulados por log (sin SMTP_HOST/SMTP_USER/SMTP_PASSWORD en el entorno)");
}

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

// Un evento INFO por petición HTTP (método, path, status, duración). La plantilla por
// defecto usa RequestPath SIN query string ni body: no se filtran datos personales.
app.UseSerilogRequestLogging();

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ReservationsHub>("/hubs/reservations");

// --- Health checks: anónimos y fuera del rate limiting (no hay limitador global) ---
// Liveness: proceso vivo, sin tocar la BD (un parpadeo de la BD no debe tumbar el contenedor).
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
// Readiness: incluye el check de la BD.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

app.Run();

// Expone Program para los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
