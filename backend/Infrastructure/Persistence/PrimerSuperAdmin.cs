using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// El alta del primer SuperAdmin de una base de producción, que no tiene a nadie que lo dé
/// de alta: el seed solo corre en Development y el panel necesita un SuperAdmin para crear
/// usuarios.
/// </summary>
/// <remarks>
/// Se dispara con <c>PrimerSuperAdmin:Email</c> y <c>PrimerSuperAdmin:Password</c> en la
/// configuración del servidor, y solo hace algo si la base no tiene ningún SuperAdmin. Con
/// uno ya creado no toca nada —ni siquiera la contraseña—, así que olvidarse las variables
/// puestas no le abre la puerta a quien las lea después. La contraseña queda como
/// provisoria: el primer ingreso obliga a cambiarla, y la que quedó escrita en el hosting
/// deja de servir.
/// </remarks>
public static class PrimerSuperAdmin
{
    public const string Seccion = "PrimerSuperAdmin";

    public enum Resultado
    {
        /// <summary>No hay variables: el caso de todos los arranques menos el primero.</summary>
        SinConfigurar,
        Creado,
        /// <summary>Ya había un SuperAdmin; las variables sobran y hay que sacarlas.</summary>
        YaExistia,
    }

    public static async Task<Resultado> AsegurarAsync(
        AppDbContext db,
        IPasswordHasher hasher,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            return Resultado.SinConfigurar;
        }

        var hayUno = await db.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Rol == RolUsuario.SuperAdmin, cancellationToken)
            .ConfigureAwait(false);

        if (hayUno)
        {
            return Resultado.YaExistia;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{Seccion}:Email no es un email.");
        }

        if (!PoliticaDePassword.EsAceptable(password))
        {
            throw new InvalidOperationException($"{Seccion}:Password: {PoliticaDePassword.Mensaje}");
        }

        // Un SuperAdmin no tiene tenant, y al arrancar no hay ninguno resuelto.
        using var _ = db.PermitirEscrituraCrossTenant();

        db.Users.Add(new User
        {
            TenantId = null,
            Email = Emails.Normalizar(email),
            Nombre = "Super Admin",
            Rol = RolUsuario.SuperAdmin,
            PasswordHash = hasher.Hash(password!),
            DebeCambiarPassword = true,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Resultado.Creado;
    }
}
