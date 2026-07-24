using Microsoft.AspNetCore.Diagnostics;
using Slotify.Domain.Exceptions;

namespace Slotify.API;

/// <summary>
/// Manejo de errores estándar: mapea cada excepción de dominio a su respuesta
/// {error, message} con el status correcto, para que los controllers no repitan
/// try/catch. Los casos con slug contextual (p. ej. 'invalid_password' al borrar
/// cuenta/negocio, o el webhook de Stripe que responde 200) conservan su catch
/// local, que gana porque se ejecuta antes de llegar aquí. Lo no mapeado se deja
/// pasar (500 genérico), nunca se filtra el detalle interno.
/// </summary>
public class DomainExceptionHandler : IExceptionHandler
{
    private static readonly Dictionary<Type, (int Status, string Error)> Map = new()
    {
        // --- 400: petición inválida ---
        [typeof(WeakPasswordException)] = (StatusCodes.Status400BadRequest, "weak_password"),
        [typeof(InvalidPasswordResetTokenException)] = (StatusCodes.Status400BadRequest, "invalid_reset_token"),
        [typeof(InvalidEmailVerificationTokenException)] = (StatusCodes.Status400BadRequest, "invalid_verification_token"),
        [typeof(EmailAlreadyVerifiedException)] = (StatusCodes.Status400BadRequest, "email_already_verified"),
        [typeof(InvalidPaginationException)] = (StatusCodes.Status400BadRequest, "invalid_pagination"),
        [typeof(BusinessNameMismatchException)] = (StatusCodes.Status400BadRequest, "name_mismatch"),
        [typeof(InvalidConfirmationModeException)] = (StatusCodes.Status400BadRequest, "invalid_confirmation_mode"),
        [typeof(InvalidCancellationCutoffException)] = (StatusCodes.Status400BadRequest, "invalid_cancellation_cutoff"),
        [typeof(InvalidNotificationSettingsException)] = (StatusCodes.Status400BadRequest, "invalid_notification_settings"),
        [typeof(InvalidBookingModeException)] = (StatusCodes.Status400BadRequest, "invalid_booking_mode"),
        [typeof(InvalidPlanException)] = (StatusCodes.Status400BadRequest, "invalid_plan"),
        [typeof(InvalidCategoryException)] = (StatusCodes.Status400BadRequest, "invalid_category"),
        [typeof(InvalidBusinessProfileException)] = (StatusCodes.Status400BadRequest, "invalid_profile"),
        [typeof(InvalidPhotoException)] = (StatusCodes.Status400BadRequest, "invalid_photo"),
        [typeof(InvalidBusinessHoursException)] = (StatusCodes.Status400BadRequest, "invalid_hours"),
        [typeof(InvalidHolidayException)] = (StatusCodes.Status400BadRequest, "invalid_holiday"),
        [typeof(InvalidGuestContactException)] = (StatusCodes.Status400BadRequest, "invalid_guest_contact"),
        [typeof(SelfBookingNotAllowedException)] = (StatusCodes.Status400BadRequest, "self_booking_not_allowed"),
        [typeof(InvalidReviewException)] = (StatusCodes.Status400BadRequest, "invalid_review"),
        [typeof(StaffEmailRequiredException)] = (StatusCodes.Status400BadRequest, "email_required"),
        [typeof(InvalidContactMessageException)] = (StatusCodes.Status400BadRequest, "invalid_contact_message"),
        [typeof(InvalidWaitlistDateException)] = (StatusCodes.Status400BadRequest, "invalid_date"),

        // --- 401: credenciales ---
        [typeof(InvalidCredentialsException)] = (StatusCodes.Status401Unauthorized, "invalid_credentials"),
        [typeof(InvalidRefreshTokenException)] = (StatusCodes.Status401Unauthorized, "invalid_refresh_token"),

        // --- 403: sin permisos ---
        [typeof(NotBusinessOwnerException)] = (StatusCodes.Status403Forbidden, "forbidden"),
        [typeof(ReservationForbiddenException)] = (StatusCodes.Status403Forbidden, "forbidden"),
        [typeof(ReviewForbiddenException)] = (StatusCodes.Status403Forbidden, "forbidden"),

        // --- 404: no existe ---
        [typeof(BusinessNotFoundException)] = (StatusCodes.Status404NotFound, "business_not_found"),
        [typeof(ServiceNotFoundException)] = (StatusCodes.Status404NotFound, "service_not_found"),
        [typeof(StaffNotFoundException)] = (StatusCodes.Status404NotFound, "staff_not_found"),
        [typeof(StaffInviteNotFoundException)] = (StatusCodes.Status404NotFound, "invite_not_found"),
        [typeof(ReservationNotFoundException)] = (StatusCodes.Status404NotFound, "reservation_not_found"),
        [typeof(ReviewNotFoundException)] = (StatusCodes.Status404NotFound, "review_not_found"),
        [typeof(HolidayNotFoundException)] = (StatusCodes.Status404NotFound, "holiday_not_found"),
        [typeof(CheckoutNotFoundException)] = (StatusCodes.Status404NotFound, "checkout_not_found"),
        [typeof(WaitlistEntryNotFoundException)] = (StatusCodes.Status404NotFound, "waitlist_entry_not_found"),

        // --- 409: conflicto con el estado actual ---
        [typeof(EmailAlreadyExistsException)] = (StatusCodes.Status409Conflict, "email_exists"),
        [typeof(BusinessHasFutureReservationsException)] = (StatusCodes.Status409Conflict, "business_has_future_reservations"),
        [typeof(PaymentRequiredException)] = (StatusCodes.Status409Conflict, "payment_required"),
        [typeof(AlreadyPremiumException)] = (StatusCodes.Status409Conflict, "already_premium"),
        [typeof(FreemiumLimitReachedException)] = (StatusCodes.Status409Conflict, "limit_reached"),
        [typeof(SlotUnavailableException)] = (StatusCodes.Status409Conflict, "slot_unavailable"),
        [typeof(OnlineBookingDisabledException)] = (StatusCodes.Status409Conflict, "online_booking_disabled"),
        [typeof(ContactBelongsToAccountException)] = (StatusCodes.Status409Conflict, "contact_belongs_to_account"),
        [typeof(CancellationWindowClosedException)] = (StatusCodes.Status409Conflict, "window_closed"),
        [typeof(ReservationConcurrencyException)] = (StatusCodes.Status409Conflict, "concurrency_conflict"),
        [typeof(ReservationNotPendingException)] = (StatusCodes.Status409Conflict, "not_pending"),
        [typeof(ReservationNotPastException)] = (StatusCodes.Status409Conflict, "not_past"),
        [typeof(ReviewNotAllowedException)] = (StatusCodes.Status409Conflict, "review_not_allowed"),
        [typeof(StaffAlreadyHasAccountException)] = (StatusCodes.Status409Conflict, "already_has_account"),
        [typeof(AlreadyOnWaitlistException)] = (StatusCodes.Status409Conflict, "already_waiting"),
        [typeof(WaitlistNotNeededException)] = (StatusCodes.Status409Conflict, "slots_available"),
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!Map.TryGetValue(exception.GetType(), out var mapping))
            return false;

        httpContext.Response.StatusCode = mapping.Status;
        // Las excepciones de validación con lista de reglas incumplidas añaden 'details'.
        object body = exception switch
        {
            WeakPasswordException weak => new { error = mapping.Error, message = exception.Message, details = weak.Errors },
            InvalidContactMessageException contact => new { error = mapping.Error, message = exception.Message, details = contact.Errors },
            _ => new { error = mapping.Error, message = exception.Message },
        };
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}
