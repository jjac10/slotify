namespace Slotify.Domain.Exceptions;

/// <summary>
/// El negocio está en modo 'solo calendario': no acepta reservas online de clientes
/// (solo el owner/staff apunta reservas desde la Agenda). HTTP 409.
/// </summary>
public class OnlineBookingDisabledException() : Exception("Este negocio no acepta reservas online.");
