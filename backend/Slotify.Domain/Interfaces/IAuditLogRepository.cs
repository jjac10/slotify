using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Registro de auditoría de reservas (ADR #14).</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
}
