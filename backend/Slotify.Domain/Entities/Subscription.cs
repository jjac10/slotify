namespace Slotify.Domain.Entities;

/// <summary>
/// Suscripción Premium de un negocio. Nace 'pending' al abrir el checkout y pasa a
/// 'active' cuando el proveedor confirma el pago (webhook de Stripe o el checkout
/// simulado del TFM); el downgrade a Free la deja 'cancelled'. El upgrade de plan ya
/// NO es directo: siempre entra por aquí (cierra el TODO "gatear tras pago real").
/// </summary>
public class Subscription
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    /// <summary>'stripe' | 'simulated'.</summary>
    public string Provider { get; set; } = null!;

    /// <summary>Id externo: session id de Stripe o el token del checkout simulado.</summary>
    public string ExternalId { get; set; } = null!;

    /// <summary>'pending' | 'active' | 'cancelled'.</summary>
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
}
