namespace Slotify.Domain.Entities;

/// <summary>
/// Relación N:M entre trabajador y servicio: qué servicios puede realizar cada
/// staff. Tabla <c>staff_services</c> (docs/DATA_MODEL.md). Un staff SIN filas
/// aquí se interpreta como "puede realizar todos los servicios" (compatibilidad
/// con el owner-as-staff y negocios de un solo trabajador).
/// </summary>
public class StaffServiceAssignment
{
    public Guid Id { get; set; }

    /// <summary>Trabajador. NOT NULL → staff(id) (ON DELETE CASCADE).</summary>
    public Guid StaffId { get; set; }
    public Staff? Staff { get; set; }

    /// <summary>Servicio. NOT NULL → services(id) (ON DELETE CASCADE).</summary>
    public Guid ServiceId { get; set; }
    public Service? Service { get; set; }
}
