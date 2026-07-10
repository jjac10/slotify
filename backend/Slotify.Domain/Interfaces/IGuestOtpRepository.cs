using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

public interface IGuestOtpRepository
{
    Task AddAsync(GuestOtpCode code, CancellationToken ct = default);

    /// <summary>El código más reciente emitido para un contacto (por blind index), o null.</summary>
    Task<GuestOtpCode?> GetLatestByContactHashAsync(string contactHash, CancellationToken ct = default);

    Task UpdateAsync(GuestOtpCode code, CancellationToken ct = default);
}
