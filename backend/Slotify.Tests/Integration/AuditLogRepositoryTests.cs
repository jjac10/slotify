using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// `audit_logs`: la auditoría debe SOBREVIVIR al hard-delete de la reserva
/// (reservation_id ON DELETE SET NULL), conservando el snapshot en old_values.
/// </summary>
public class AuditLogRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public AuditLogRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task AuditLog_SurvivesReservationHardDelete_WithReservationIdSetToNull()
    {
        var (reservationId, actorId) = await SeedReservationAsync();
        var repo = new AuditLogRepository(_db);

        await repo.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            Action = "cancelled",
            ActorId = actorId,
            ActorType = "owner",
            OldValues = """{"status":"pending"}""",
        });

        // Hard-delete de la reserva.
        await _db.Reservations.Where(r => r.Id == reservationId).ExecuteDeleteAsync();

        await using var verify = _fixture.CreateContext();
        var audit = await verify.AuditLogs.AsNoTracking().SingleAsync(a => a.Action == "cancelled");
        Assert.Null(audit.ReservationId);             // SET NULL: la referencia se anula...
        Assert.NotNull(audit.OldValues);              // ...pero el snapshot persiste
        Assert.Contains("pending", audit.OldValues!); // (jsonb normaliza el formato, comparamos contenido)
    }

    private async Task<(Guid reservationId, Guid actorId)> SeedReservationAsync()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"o-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "O", Type = "owner" };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Biz" };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = owner.Id, Role = "owner", Name = "O" };
        var service = new Service { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Corte", DurationMinutes = 30 };
        var guest = new Guest { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Juan", PhoneHash = $"ph-{Guid.NewGuid():N}" };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            ServiceId = service.Id,
            StaffId = staff.Id,
            GuestId = guest.Id,
            StartTime = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 6, 20, 10, 30, 0, DateTimeKind.Utc),
            Status = "pending",
        };
        _db.AddRange(owner, business, staff, service, guest, reservation);
        await _db.SaveChangesAsync();
        return (reservation.Id, owner.Id);
    }
}
