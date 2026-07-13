using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Borrado de negocio end-to-end: el owner con confirmación máxima (nombre exacto +
/// contraseña) y el admin de plataforma (Admin:Email) desde /admin. El cascade elimina
/// servicios, staff, horarios, reservas, reseñas, invitados y notificaciones.
/// </summary>
public class BusinessDeletionEndpointsTests : IClassFixture<BusinessDeletionEndpointsTests.AdminFactory>
{
    private const string AdminEmail = "admin@slotify.test";
    private const string Password = "SecurePass123!";

    private readonly AdminFactory _factory;
    private readonly HttpClient _client;

    public BusinessDeletionEndpointsTests(AdminFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid businessId, string name, HttpClient owner)> RegisterBusinessAsync()
    {
        var name = $"Barbería {Guid.NewGuid():N}"[..20];
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", Password, "Pepe", name);
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.BusinessId!.Value, name, owner);
    }

    private async Task<HttpClient> LoginAdminAsync()
    {
        // El admin es un usuario normal cuyo email coincide con Admin:Email.
        var register = await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest(AdminEmail, Password, "Admin"));
        AuthResult? auth;
        if (register.IsSuccessStatusCode)
            auth = await register.Content.ReadFromJsonAsync<AuthResult>();
        else // ya registrado por otro test de la clase
            auth = await (await _client.PostAsJsonAsync("/auth/login",
                new LoginRequest(AdminEmail, Password))).Content.ReadFromJsonAsync<AuthResult>();

        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return admin;
    }

    [Fact]
    public async Task Owner_DeletesBusiness_WithExactNameAndPassword_CascadeRemovesEverything()
    {
        var (businessId, name, owner) = await RegisterBusinessAsync();
        (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).EnsureSuccessStatusCode();

        var res = await owner.PostAsJsonAsync($"/businesses/{businessId}/delete",
            new DeleteBusinessRequest(name, Password));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        // El negocio y sus datos han desaparecido (el listado público de servicios
        // devuelve 200 con lista vacía para ids desconocidos: se comprueba en BD).
        var services = await (await _client.GetAsync($"/businesses/{businessId}/services"))
            .Content.ReadFromJsonAsync<List<ServiceResponse>>();
        Assert.Empty(services!);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.False(await db.Businesses.AnyAsync(b => b.Id == businessId));
        Assert.False(await db.Staff.AnyAsync(s => s.BusinessId == businessId));
        Assert.False(await db.Services.AnyAsync(s => s.BusinessId == businessId));
    }

    [Fact]
    public async Task Owner_DeleteWithWrongName_Returns400_AndNothingIsDeleted()
    {
        var (businessId, _, owner) = await RegisterBusinessAsync();

        var res = await owner.PostAsJsonAsync($"/businesses/{businessId}/delete",
            new DeleteBusinessRequest("Otro Nombre", Password));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("name_mismatch", await res.Content.ReadAsStringAsync());
        (await owner.GetAsync($"/businesses/{businessId}/services")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Owner_DeleteWithWrongPassword_Returns400()
    {
        var (businessId, name, owner) = await RegisterBusinessAsync();

        var res = await owner.PostAsJsonAsync($"/businesses/{businessId}/delete",
            new DeleteBusinessRequest(name, "Incorrecta123!"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_password", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Owner_DeleteWithFutureReservation_Returns409()
    {
        var (businessId, name, owner) = await RegisterBusinessAsync();
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
            (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
                businessId, service!.Id, staffId, DateTime.UtcNow.AddDays(3).Date.AddHours(10), "Juan", "+34910000021", null)))
                .EnsureSuccessStatusCode();
        }

        var res = await owner.PostAsJsonAsync($"/businesses/{businessId}/delete",
            new DeleteBusinessRequest(name, Password));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("business_has_future_reservations", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Intruder_CannotDeleteSomeoneElsesBusiness()
    {
        var (businessId, name, _) = await RegisterBusinessAsync();
        var (_, _, intruder) = await RegisterBusinessAsync(); // otro owner autenticado

        var res = await intruder.PostAsJsonAsync($"/businesses/{businessId}/delete",
            new DeleteBusinessRequest(name, Password));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Admin_ListsAndDeletesBusiness_EvenWithFutureReservations()
    {
        var (businessId, name, owner) = await RegisterBusinessAsync();
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
            (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
                businessId, service!.Id, staffId, DateTime.UtcNow.AddDays(3).Date.AddHours(11), "Juan", "+34910000022", null)))
                .EnsureSuccessStatusCode();
        }
        var admin = await LoginAdminAsync();

        // El directorio de moderación encuentra el negocio por nombre.
        var page = await (await admin.GetAsync($"/admin/businesses?q={Uri.EscapeDataString(name)}"))
            .Content.ReadFromJsonAsync<PagedResponse<AdminBusinessResponse>>();
        Assert.Contains(page!.Items, b => b.Id == businessId);

        // Y lo elimina aunque tenga reservas futuras (moderación).
        var res = await admin.PostAsync($"/admin/businesses/{businessId}/delete", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        using var check = _factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.False(await db2.Businesses.AnyAsync(b => b.Id == businessId));
    }

    [Fact]
    public async Task NonAdmin_GetsForbiddenOnAdminEndpoints()
    {
        var (_, _, owner) = await RegisterBusinessAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/admin/businesses")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.PostAsync($"/admin/businesses/{Guid.NewGuid()}/delete", null)).StatusCode);
    }

    /// <summary>Factory con el email del admin de plataforma configurado.</summary>
    public sealed class AdminFactory : SlotifyApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Admin:Email", AdminEmail);
        }
    }
}
