using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>Datos mínimos de una reserva para despachar un aviso (sirve también tras cancelar/borrar).</summary>
public record NotificationContext(Guid BusinessId, Guid ReservationId, Guid? UserId, Guid? GuestId, DateTime StartTime);

/// <summary>
/// Despacha avisos a clientes (reserva creada/confirmada/reprogramada/cancelada y
/// recordatorio) según la configuración del negocio (canales email/WhatsApp y antelación
/// del recordatorio). El envío real lo hace un <see cref="INotificationSender"/> swappable
/// (en el TFM, simulado y registrado). Es best-effort: nunca rompe la acción del usuario.
/// </summary>
public class NotificationService(
    IBusinessRepository businesses,
    IReservationRepository reservations,
    IAuthRepository users,
    IGuestRepository guests,
    INotificationRepository notifications,
    INotificationSender sender,
    ICryptoService crypto)
{
    /// <summary>Cap de la consulta de recordatorios (acota la ventana de reservas a revisar).</summary>
    private static readonly TimeSpan ReminderQueryCap = TimeSpan.FromDays(30);

    /// <summary>Despacha un evento inmediato (created/confirmed/rescheduled/cancelled) para una reserva.</summary>
    public async Task DispatchEventAsync(NotificationContext ctx, string eventType, CancellationToken ct = default)
    {
        try
        {
            var business = await businesses.GetByIdAsync(ctx.BusinessId, ct);
            if (business is not null)
                await SendForChannelsAsync(business, ctx, eventType, ct);
        }
        catch
        {
            // Best-effort: un fallo notificando jamás debe tumbar la operación del usuario.
        }
    }

    /// <summary>
    /// Envía los recordatorios de cita que ya entran en la ventana de antelación de cada
    /// negocio y aún no se han enviado. Devuelve cuántas reservas se recordaron. Lo invoca
    /// el servicio en segundo plano de forma periódica.
    /// </summary>
    public async Task<int> DispatchDueRemindersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var candidates = await reservations.ListUpcomingForReminderAsync(nowUtc, nowUtc.Add(ReminderQueryCap), ct);
        var reminded = 0;
        foreach (var r in candidates)
        {
            var business = r.Business;
            if (business is null || business.ReminderHoursBefore <= 0 || !HasAnyChannel(business))
                continue;
            // Aún no entra en la ventana de antelación del negocio.
            if ((r.StartTime - nowUtc).TotalHours > business.ReminderHoursBefore)
                continue;
            if (await notifications.ExistsForReservationAsync(r.Id, "reminder", ct))
                continue;

            var ctx = new NotificationContext(r.BusinessId, r.Id, r.UserId, r.GuestId, r.StartTime);
            await SendForChannelsAsync(business, ctx, "reminder", ct);
            reminded++;
        }
        return reminded;
    }

    private static bool HasAnyChannel(Business b) => b.NotifyByEmail || b.NotifyByWhatsapp;

    private async Task SendForChannelsAsync(Business business, NotificationContext ctx, string eventType, CancellationToken ct)
    {
        if (!HasAnyChannel(business))
            return;

        var (email, phone) = await ResolveContactAsync(ctx, ct);
        var body = BuildBody(business.Name, eventType, ctx.StartTime);

        if (business.NotifyByEmail && !string.IsNullOrWhiteSpace(email))
            await SendOneAsync(ctx, "email", email!, eventType, body, ct);
        if (business.NotifyByWhatsapp && !string.IsNullOrWhiteSpace(phone))
            await SendOneAsync(ctx, "whatsapp", phone!, eventType, body, ct);
    }

    private async Task SendOneAsync(
        NotificationContext ctx, string channel, string recipient, string eventType, string body, CancellationToken ct)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            BusinessId = ctx.BusinessId,
            ReservationId = ctx.ReservationId,
            Channel = channel,
            EventType = eventType,
            Recipient = recipient,
            Body = body,
            Status = "logged",
            CreatedAt = DateTime.UtcNow,
        };
        await sender.SendAsync(notification, ct);
        await notifications.AddAsync(notification, ct);
    }

    /// <summary>Resuelve el email/teléfono del cliente (usuario registrado o invitado, descifrando).</summary>
    private async Task<(string? Email, string? Phone)> ResolveContactAsync(NotificationContext ctx, CancellationToken ct)
    {
        if (ctx.UserId is { } userId)
        {
            var user = await users.GetByIdAsync(userId, ct);
            return (user?.Email, user?.Phone);
        }
        if (ctx.GuestId is { } guestId)
        {
            var guest = await guests.GetByIdAsync(guestId, ct);
            var email = guest?.EmailEncrypted is { } e ? crypto.Decrypt(e) : null;
            var phone = guest?.PhoneEncrypted is { } p ? crypto.Decrypt(p) : null;
            return (email, phone);
        }
        return (null, null);
    }

    private static string BuildBody(string businessName, string eventType, DateTime startUtc)
    {
        var when = startUtc.ToString("dd/MM/yyyy HH:mm");
        return eventType switch
        {
            "created" => $"Tu reserva en {businessName} para el {when} ha sido registrada.",
            "confirmed" => $"Tu reserva en {businessName} para el {when} ha sido confirmada.",
            "rescheduled" => $"Tu reserva en {businessName} se ha reprogramado al {when}.",
            "cancelled" => $"Tu reserva en {businessName} del {when} ha sido cancelada.",
            "reminder" => $"Recordatorio: tienes una reserva en {businessName} el {when}.",
            _ => $"Actualización de tu reserva en {businessName} ({when}).",
        };
    }
}
