using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Un dominio propio que una automotora quiere usar para su sitio público.
/// </summary>
/// <remarks>
/// Es la única fuente de verdad para resolver el tenant por <c>Host</c>. Antes el dominio
/// era una columna de <c>tenants</c> que cargaba el SuperAdmin a mano, y eso tenía dos
/// problemas: la automotora dependía de que alguien de la plataforma le tocara la fila, y
/// nada verificaba que el dominio fuera realmente suyo. Con una tabla aparte, el dueño lo
/// da de alta solo y prueba que le pertenece publicando un TXT.
/// <para>
/// Un tenant puede tener varios —el apex, un alias viejo que todavía recibe tráfico—, pero
/// solo uno es <see cref="EsPrincipal"/>: es el que se usa para las URLs canónicas del
/// sitemap, donde tener dos respuestas posibles sería un problema de SEO.
/// </para>
/// </remarks>
public class DominioDeTenant : ITenantEntity, ICreatedAt
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Normalizado: minúsculas y sin <c>www.</c>. Único en todo el sistema.</summary>
    public required string Dominio { get; set; }

    public EstadoDeDominio Estado { get; set; } = EstadoDeDominio.Pendiente;

    /// <summary>
    /// Lo que tiene que aparecer en el TXT del dominio para probar que quien lo dio de alta
    /// lo controla. Se genera al crear y no se rota: rotarlo invalidaría un DNS que ya está
    /// puesto y correcto.
    /// </summary>
    public required string TokenDeVerificacion { get; set; }

    /// <summary>El que manda para las URLs canónicas. Uno solo por tenant.</summary>
    public bool EsPrincipal { get; set; }

    /// <summary>UTC. Cuándo verificó por primera vez.</summary>
    public DateTime? VerificadoEn { get; set; }

    /// <summary>UTC. Último intento, haya salido bien o mal.</summary>
    public DateTime? UltimaVerificacion { get; set; }

    /// <summary>
    /// Fallos consecutivos de la reverificación. Un dominio ya verificado no se apaga al
    /// primer fallo: puede ser un DNS lento o una zona en propagación, y bajarle el sitio a
    /// una automotora por eso sería peor que esperar.
    /// </summary>
    public int VerificacionesFallidas { get; set; }

    /// <summary>Qué pasó en el último intento fallido, en castellano y para mostrar.</summary>
    public string? UltimoError { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
