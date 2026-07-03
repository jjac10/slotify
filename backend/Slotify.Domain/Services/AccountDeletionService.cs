using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Borrado de cuenta (derecho de supresión RGPD, prometido en la página de privacidad).
/// Exige la contraseña actual como confirmación (misma verificación que el login).
///
/// Semántica por rol:
/// - Cliente: sus reservas históricas NO se borran (la agenda del negocio no pierde
///   registros); en su lugar el user queda como "tombstone" anonimizado ("Usuario
///   eliminado", email sintético, sin teléfono, hash aleatorio) al que siguen
///   apuntando. Se borran reseñas y tokens, se cancelan sus reservas futuras y se
///   anonimizan sus fichas de invitado (cifrados eliminados).
/// - Owner: si su negocio tiene reservas futuras activas → excepción (409). Si no,
///   el negocio se elimina por completo (servicios, staff, horarios, reservas…).
/// - Empleado: se desvincula de su staff (la fila queda como empleado sin cuenta).
/// </summary>
public class AccountDeletionService(
    IAuthRepository auth,
    IPasswordHasher hasher,
    IBusinessRepository businesses,
    IAccountDeletionRepository deletion)
{
    public const string AnonymizedName = "Usuario eliminado";

    public async Task DeleteAccountAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await auth.GetByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        // Confirmación con la contraseña actual (igual que valida el login).
        if (!hasher.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var nowUtc = DateTime.UtcNow;

        // Owner: solo puede borrar si su(s) negocio(s) no tienen reservas futuras activas.
        var businessIdsToDelete = new List<Guid>();
        if (user.Type == AuthService.OwnerType)
        {
            foreach (var business in await businesses.ListByOwnerAsync(user.Id, ct))
            {
                if (await deletion.HasFutureActiveReservationsAsync(business.Id, nowUtc, ct))
                    throw new BusinessHasFutureReservationsException();
                businessIdsToDelete.Add(business.Id);
            }
        }

        Anonymize(user, nowUtc);
        await deletion.DeleteAccountAsync(user, businessIdsToDelete, nowUtc, ct);
    }

    /// <summary>
    /// Deja el user como tombstone sin datos personales. La fila se conserva porque
    /// las reservas históricas la referencian (CHECK user_or_guest impide poner el
    /// user_id a NULL), pero ya no contiene nada identificable y el email original
    /// queda libre para un futuro re-registro.
    /// </summary>
    private void Anonymize(User user, DateTime nowUtc)
    {
        user.Name = AnonymizedName;
        user.Email = $"deleted-{user.Id:N}@anon.slotify.invalid"; // único (UNIQUE email) y no personal
        user.Phone = null;
        user.PasswordHash = hasher.Hash(Guid.NewGuid().ToString("N")); // nadie puede volver a entrar
        user.Status = "deleted";
        user.EmailVerifiedAt = null;
        user.DeletedAt = nowUtc;
        user.UpdatedAt = nowUtc;
    }
}
