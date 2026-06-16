using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;
using Scalar.AspNetCore;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;
using Slotify.Infrastructure.Security;

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
builder.Services.AddScoped<IGuestRepository, GuestRepository>();
builder.Services.AddScoped<IReservationRepository, ReservationRepository>();
builder.Services.AddScoped<IBusinessHourRepository, BusinessHourRepository>();
builder.Services.AddScoped<IBusinessHolidayRepository, BusinessHolidayRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IFreemiumLimitService, FreemiumLimitService>();
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<ServiceService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<BusinessScheduleService>();
builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<AuthService>();

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

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expone Program para los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
