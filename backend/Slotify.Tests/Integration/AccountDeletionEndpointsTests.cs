using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Services;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Borrado de cuenta end-to-end (DELETE /auth/me, derecho de supresión RGPD):
/// cliente (anonimiza histórico, cancela futuras, borra reseñas/tokens/cifrados),
/// owner (borra el negocio entero, o 409 si tiene reservas futuras de clientes)
/// y empleado (desvincula su staff). Autorización y confirmación por contraseña.
/// </summary>
public class AccountDeletionEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private const string Password = "SecurePass123!";

    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    // Hora distinta por reserva sembrada para no chocar con el anti-doble-booking del staff.
    private static int _seq;

    private static HttpRequestMessage DeleteMeRequest(string? password) =>
        new(HttpMethod.Delete, "/auth/me") { Content = JsonContent.Create(new { password }) };

    private async Task<(Guid businessId, string businessName, Guid serviceId, Guid staffId, string email, HttpClient client, Guid userId)> SetupOwnerAsync()
    {
        var name = $"Barberia {Guid.NewGuid():N}";
        var email = $"owner-{Guid.NewGuid():N}@test.local";
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest(email, Password, "Pepe", name))).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, name, service!.Id, staffId, email, owner, auth.UserId);
    }

    private async Task<(Guid userId, string email, HttpClient client)> RegisterCustomerAsync()
    {
        var email = $"cust-{Guid.NewGuid():N}@test.local";
        var auth = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest(email, Password, "Ana"))).Content.ReadFromJsonAsync<AuthResult>();
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.UserId, email, c);
    }

    /// <summary>Inserta una reserva confirmada directamente en BD (pasada o futura).</summary>
    private async Task<Guid> SeedReservationAsync(Guid businessId, Guid serviceId, Guid staffId, Guid userId, bool past)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var slot = Interlocked.Increment(ref _seq);
        var start = (past ? DateTime.UtcNow.AddDays(-3) : DateTime.UtcNow.AddDays(3)).AddHours(slot);
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(), BusinessId = businessId, ServiceId = serviceId, StaffId = staffId,
            UserId = userId, Status = "confirmed", StartTime = start, EndTime = start.AddMinutes(30),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<HttpStatusCode> LoginStatusAsync(string email, string password = Password) =>
        (await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password))).StatusCode;

    [Fact]
    public async Task DeleteMe_WithoutToken_Returns401()
    {
        var res = await _client.SendAsync(DeleteMeRequest(Password));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_WrongPassword_Returns400_AndAccountSurvives()
    {
        var (_, email, customer) = await RegisterCustomerAsync();

        var res = await customer.SendAsync(DeleteMeRequest("WrongPass123!"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("invalid_password", body);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(email)); // la cuenta sigue viva
    }

    [Fact]
    public async Task Customer_DeletesAccount_AnonymizesHistoryCancelsFutureAndErasesPersonalData()
    {
        var (businessId, businessName, serviceId, staffId, _, owner, _) = await SetupOwnerAsync();
        var (userId, email, customer) = await RegisterCustomerAsync();
        var pastId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);
        var futureId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: false);

        // Reseña del cliente (el negocio pasa a tener media 5.0)…
        (await customer.PostAsJsonAsync($"/reservations/{pastId}/review", new CreateReviewRequest(5, "Genial")))
            .EnsureSuccessStatusCode();

        // …ficha de invitado vinculada a su cuenta (contacto cifrado) y una notificación con su email.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            db.Guests.Add(new Guest
            {
                Id = Guid.NewGuid(), BusinessId = businessId, Name = "Ana",
                PhoneEncrypted = "cifrado-telefono", PhoneHash = $"hash-{Guid.NewGuid():N}",
                UserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), BusinessId = businessId, ReservationId = pastId,
                Channel = "email", EventType = "created", Recipient = email, Body = "Reserva creada",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Borrado de cuenta
        var res = await customer.SendAsync(DeleteMeRequest(Password));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        // Ya no puede volver a entrar.
        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync(email));

        // La agenda del negocio conserva la reserva histórica, anonimizada.
        var agenda = await (await owner.GetAsync($"/businesses/{businessId}/reservations"))
            .Content.ReadFromJsonAsync<PagedResponse<ReservationResponse>>();
        var past = Assert.Single(agenda!.Items, r => r.Id == pastId);
        Assert.Equal(AccountDeletionService.AnonymizedName, past.ClientName);
        Assert.DoesNotContain(agenda.Items, r => r.Id == futureId); // la futura quedó cancelada (no aparece)

        // Reseña borrada y media del negocio recalculada.
        var reviews = await (await _client.GetAsync($"/businesses/{businessId}/reviews"))
            .Content.ReadFromJsonAsync<List<ReviewResponse>>();
        Assert.Empty(reviews!);
        var businesses = await (await _client.GetAsync($"/public/businesses?q={Uri.EscapeDataString(businessName)}"))
            .Content.ReadFromJsonAsync<PagedResponse<BusinessResponse>>();
        Assert.Null(businesses!.Items.First(b => b.Id == businessId).Rating);

        // BD: tombstone sin datos personales, tokens fuera, invitado anonimizado, futura cancelada.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();

            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.Equal(AccountDeletionService.AnonymizedName, user.Name);
            Assert.Equal("deleted", user.Status);
            Assert.NotEqual(email, user.Email);
            Assert.Null(user.Phone);
            Assert.NotNull(user.DeletedAt);

            Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == userId));
            Assert.False(await db.PasswordResetTokens.AnyAsync(t => t.UserId == userId));
            Assert.False(await db.EmailVerificationTokens.AnyAsync(t => t.UserId == userId));

            var future = await db.Reservations.SingleAsync(r => r.Id == futureId);
            Assert.Equal("cancelled", future.Status);
            Assert.NotNull(future.CancelledAt);

            // La ficha de invitado ya no tiene cifrados ni vínculo con el user.
            Assert.False(await db.Guests.AnyAsync(g => g.UserId == userId));
            var guest = await db.Guests.SingleAsync(g => g.BusinessId == businessId && g.Name == AccountDeletionService.AnonymizedName);
            Assert.Null(guest.PhoneEncrypted);
            Assert.Null(guest.EmailEncrypted);

            // La notificación registrada ya no expone su email.
            var notification = await db.Notifications.SingleAsync(n => n.ReservationId == pastId);
            Assert.DoesNotContain(email, notification.Recipient);
        }
    }

    [Fact]
    public async Task Owner_WithFutureCustomerReservations_Returns409_AndNothingIsDeleted()
    {
        var (businessId, _, serviceId, staffId, ownerEmail, owner, _) = await SetupOwnerAsync();
        var (customerId, _, _) = await RegisterCustomerAsync();
        await SeedReservationAsync(businessId, serviceId, staffId, customerId, past: false);

        var res = await owner.SendAsync(DeleteMeRequest(Password));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("business_has_future_reservations", body);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(ownerEmail)); // sigue todo en pie

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.True(await db.Businesses.AnyAsync(b => b.Id == businessId));
    }

    [Fact]
    public async Task Owner_WithoutFutureReservations_DeletesBusinessCompletely()
    {
        var (businessId, _, serviceId, staffId, ownerEmail, owner, _) = await SetupOwnerAsync();
        var (customerId, customerEmail, _) = await RegisterCustomerAsync();
        await SeedReservationAsync(businessId, serviceId, staffId, customerId, past: true); // histórica: no bloquea

        var res = await owner.SendAsync(DeleteMeRequest(Password));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync(ownerEmail));
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync(customerEmail)); // el cliente no se ve afectado

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.False(await db.Businesses.AnyAsync(b => b.Id == businessId));
        Assert.False(await db.Staff.AnyAsync(s => s.BusinessId == businessId));
        Assert.False(await db.Services.AnyAsync(s => s.BusinessId == businessId));
        Assert.False(await db.Reservations.AnyAsync(r => r.BusinessId == businessId));
        Assert.False(await db.BusinessHours.AnyAsync(h => h.BusinessId == businessId));
    }

    [Fact]
    public async Task Employee_DeletesAccount_UnlinksStaffAndKeepsRosterRow()
    {
        var (businessId, _, _, _, _, owner, _) = await SetupOwnerAsync();
        // El plan Free solo permite 1 trabajador; premium para poder añadir un empleado.
        (await owner.PutAsJsonAsync($"/businesses/{businessId}/plan", new { code = "premium" })).EnsureSuccessStatusCode();

        var employeeEmail = $"emp-{Guid.NewGuid():N}@test.local";
        var member = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff",
            new CreateStaffRequest("Marta", employeeEmail, null))).Content.ReadFromJsonAsync<StaffResponse>();
        var invite = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff/{member!.Id}/invite", new { }))
            .Content.ReadFromJsonAsync<StaffInviteResponse>();
        var empAuth = await (await _client.PostAsJsonAsync($"/auth/staff-invite/{invite!.Token}/accept",
            new AcceptStaffInviteRequest(Password))).Content.ReadFromJsonAsync<AuthResult>();
        var employee = _factory.CreateClient();
        employee.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empAuth!.AccessToken);

        var res = await employee.SendAsync(DeleteMeRequest(Password));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, await LoginStatusAsync(employeeEmail));

        // La fila de staff sigue en el equipo del negocio, pero sin cuenta vinculada.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffRow = await db.Staff.SingleAsync(s => s.Id == member.Id);
        Assert.Null(staffRow.UserId);
        Assert.Null(staffRow.InviteToken);
        Assert.Equal("Marta", staffRow.Name); // la ficha del equipo es del negocio: se conserva
    }
}
