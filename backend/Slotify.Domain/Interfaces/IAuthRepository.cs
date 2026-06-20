using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Persistencia relacionada con autenticación.</summary>
public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Da de alta un usuario (p. ej. customer, sin negocio).</summary>
    Task AddUserAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Persiste atómicamente el alta de un propietario: user + negocio + owner-staff
    /// + su horario semanal por defecto.
    /// </summary>
    Task RegisterOwnerAsync(User user, Business business, Staff ownerStaff, IEnumerable<BusinessHour> hours, CancellationToken ct = default);
}
