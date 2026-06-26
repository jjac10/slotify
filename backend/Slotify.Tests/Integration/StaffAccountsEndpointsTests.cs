using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Cuentas de empleado end-to-end: el owner invita a un empleado (con email) → token;
/// el empleado acepta fijando contraseña → queda logueado como 'staff' de ese negocio;
/// su agenda solo muestra SUS reservas.
/// </summary>
public class StaffAccountsEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Owner_InvitesEmployee_AcceptsAndLogsInAsStaff_SeesOnlyOwnReservations()
    {
        // Owner + negocio + servicio
        var ownerAuth = await (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var businessId = ownerAuth!.BusinessId!.Value;
        Assert.Equal("owner", ownerAuth.BusinessRole);
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);
        // El plan Free solo permite 1 trabajador (el owner); subimos a premium para añadir empleados.
        (await owner.PutAsJsonAsync($"/businesses/{businessId}/plan", new { code = "premium" })).EnsureSuccessStatusCode();
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        // Empleado con email + invitación → token
        var employeeEmail = $"emp-{Guid.NewGuid():N}@test.local";
        var emp = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff",
            new CreateStaffRequest("Marta", employeeEmail, null))).Content.ReadFromJsonAsync<StaffResponse>();
        var invite = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff/{emp!.Id}/invite", new { }))
            .Content.ReadFromJsonAsync<StaffInviteResponse>();
        Assert.False(string.IsNullOrWhiteSpace(invite!.Token));

        // La info de la invitación es pública (para la pantalla de aceptar)
        var info = await (await _client.GetAsync($"/auth/staff-invite/{invite.Token}")).Content.ReadFromJsonAsync<StaffInviteInfoResponse>();
        Assert.Equal(employeeEmail, info!.Email);
        Assert.Equal("Marta", info.StaffName);

        // El empleado acepta fijando contraseña → queda logueado como staff del negocio
        var accept = await _client.PostAsJsonAsync($"/auth/staff-invite/{invite.Token}/accept", new AcceptStaffInviteRequest("EmpSecure123!"));
        Assert.Equal(HttpStatusCode.Created, accept.StatusCode);
        var empAuth = await accept.Content.ReadFromJsonAsync<AuthResult>();
        Assert.Equal(businessId, empAuth!.BusinessId);
        Assert.Equal("staff", empAuth.BusinessRole);

        // Login posterior también resuelve negocio + rol staff
        var login = await (await _client.PostAsJsonAsync("/auth/login", new LoginRequest(employeeEmail, "EmpSecure123!")))
            .Content.ReadFromJsonAsync<AuthResult>();
        Assert.Equal(businessId, login!.BusinessId);
        Assert.Equal("staff", login.BusinessRole);

        // El token ya no sirve dos veces
        var reaccept = await _client.PostAsJsonAsync($"/auth/staff-invite/{invite.Token}/accept", new AcceptStaffInviteRequest("Otra123456!"));
        Assert.Equal(HttpStatusCode.NotFound, reaccept.StatusCode);

        // Agenda del empleado: solo SUS reservas. Sembramos una del owner-staff y otra del empleado.
        Guid ownerStaffId, empStaffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            ownerStaffId = await db.Staff.Where(s => s.BusinessId == businessId && s.Role == "owner").Select(s => s.Id).FirstAsync();
            empStaffId = emp.Id;
            var baseStart = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);
            var clientId = ownerAuth.UserId; // un user válido como "cliente" de las reservas sembradas
            db.Reservations.AddRange(
                new() { Id = Guid.NewGuid(), BusinessId = businessId, ServiceId = service!.Id, StaffId = ownerStaffId, UserId = clientId, Status = "confirmed", StartTime = baseStart, EndTime = baseStart.AddMinutes(30), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), BusinessId = businessId, ServiceId = service.Id, StaffId = empStaffId, UserId = clientId, Status = "confirmed", StartTime = baseStart.AddHours(2), EndTime = baseStart.AddHours(2).AddMinutes(30), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var employee = _factory.CreateClient();
        employee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empAuth.AccessToken);
        var empAgenda = await (await employee.GetAsync($"/businesses/{businessId}/reservations"))
            .Content.ReadFromJsonAsync<List<ReservationResponse>>();
        Assert.All(empAgenda!, r => Assert.Equal(empStaffId, r.StaffId)); // solo las suyas
        Assert.Single(empAgenda!);

        // El owner sí ve las dos.
        var ownerAgenda = await (await owner.GetAsync($"/businesses/{businessId}/reservations"))
            .Content.ReadFromJsonAsync<List<ReservationResponse>>();
        Assert.Equal(2, ownerAgenda!.Count);
    }

    [Fact]
    public async Task Invite_StaffWithoutEmail_Returns400()
    {
        var ownerAuth = await (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var businessId = ownerAuth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);
        (await owner.PutAsJsonAsync($"/businesses/{businessId}/plan", new { code = "premium" })).EnsureSuccessStatusCode();
        var emp = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff",
            new CreateStaffRequest("SinEmail", null, null))).Content.ReadFromJsonAsync<StaffResponse>();

        var res = await owner.PostAsJsonAsync($"/businesses/{businessId}/staff/{emp!.Id}/invite", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
