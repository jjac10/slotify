namespace Slotify.Domain.Entities;

/// <summary>
/// Trabajador de un negocio. El owner ES un staff (role='owner'), creado
/// automáticamente con el negocio: así toda reserva tiene staff_id no nulo y no
/// hay lógica polimórfica owner/empleado. Schema: docs/DATA_MODEL.md (staff).
/// </summary>
public class Staff
{
    public Guid Id { get; set; }

    /// <summary>Negocio al que pertenece. NOT NULL → businesses(id) (ON DELETE CASCADE).</summary>
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>User enlazado (owner o empleado con cuenta). NULL si empleado sin login (ON DELETE SET NULL).</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>'owner' o 'employee'.</summary>
    public string Role { get; set; } = "employee";

    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>'active', 'inactive', etc.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
}
