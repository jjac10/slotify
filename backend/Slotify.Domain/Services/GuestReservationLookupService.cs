using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Permite a un invitado (sin cuenta) ver sus reservas por teléfono o email: normaliza
/// el contacto, calcula el blind index (igual que al reservar) y busca los guests que
/// coinciden y sus reservas. No expone datos de otros invitados.
///
/// TODO (seguridad/privacidad): ahora basta con conocer el teléfono/email para ver sus
/// reservas. Antes de producción hay que **verificar la identidad**: enviar un código por
/// SMS al teléfono (o email al correo) y exigirlo para devolver las reservas. Sin esa
/// verificación, cualquiera que sepa tu número podría ver tus citas.
/// </summary>
public class GuestReservationLookupService(
    IReservationRepository reservations,
    IGuestRepository guests,
    IBlindIndex blindIndex)
{
    public async Task<IReadOnlyList<ReservationResponse>> LookupAsync(string? contact, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contact))
            return [];

        // Email si contiene '@'; si no, teléfono. La normalización debe coincidir con la del alta.
        var normalized = contact.Contains('@')
            ? ContactNormalizer.NormalizeEmail(contact)
            : ContactNormalizer.NormalizePhone(contact);

        var hash = blindIndex.Compute(normalized);
        var guestIds = await guests.FindIdsByContactHashAsync(hash, ct);
        if (guestIds.Count == 0)
            return [];

        var list = await reservations.ListByGuestIdsAsync(guestIds, ct);
        return list.Select(ReservationResponse.From).ToList();
    }
}
