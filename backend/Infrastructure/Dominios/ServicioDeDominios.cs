using System.Security.Cryptography;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Dominios;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Infrastructure.Dominios;

/// <summary>
/// Alta y verificación de dominios propios, sin que nadie de la plataforma intervenga.
/// </summary>
/// <remarks>
/// La verificación es un TXT en <c>_automotora.&lt;dominio&gt;</c> con el token de la fila.
/// Se eligió TXT y no "que el dominio ya apunte acá" porque son dos cosas distintas y
/// conviene poder hacerlas en ese orden: primero se prueba la propiedad, después se mueve
/// el tráfico. Al revés, la automotora tendría que apagar su sitio actual para poder
/// empezar el alta.
/// </remarks>
public sealed class ServicioDeDominios : IServicioDeDominios
{
    private const string ErrorSinTxt =
        "No encontramos el TXT de verificación. Si lo acabás de crear, el DNS puede tardar "
        + "unos minutos en propagarse.";

    private const string ErrorTxtDistinto =
        "Hay un TXT en ese nombre pero con otro valor. Revisá que sea exactamente el token "
        + "de esta pantalla.";

    private const string ErrorDeConsulta =
        "No pudimos consultar el DNS en este momento. No quiere decir que esté mal: probá de nuevo en un rato.";

    private readonly AppDbContext _db;
    private readonly IConsultaDns _dns;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _reloj;
    private readonly OpcionesDeDominios _opciones;

    public ServicioDeDominios(
        AppDbContext db,
        IConsultaDns dns,
        ITenantContext tenantContext,
        TimeProvider reloj,
        IOptions<OpcionesDeDominios> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _db = db;
        _dns = dns;
        _tenantContext = tenantContext;
        _reloj = reloj;
        _opciones = opciones.Value;
    }

    public async Task<IReadOnlyList<DominioDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var dominios = await _db.Dominios
            .OrderByDescending(d => d.EsPrincipal)
            .ThenBy(d => d.Dominio)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return dominios.Select(ADto).ToList();
    }

    public async Task<ResultadoDeDominio> AgregarAsync(
        string dominio,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantDelRequest();
        var normalizado = NombresDeDominio.Normalizar(dominio);

        if (!NombresDeDominio.EsValido(normalizado))
        {
            return ResultadoDeDominio.Rechazado("Ese no parece un dominio válido.");
        }

        // Sin filtro de tenant a propósito: un dominio resuelve a un solo sitio en toda
        // internet, así que si lo tiene otra automotora tampoco puede tenerlo esta. Lo que
        // se responde no revela de quién es.
        var tomado = await _db.Dominios
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Dominio == normalizado, cancellationToken)
            .ConfigureAwait(false);

        if (tomado)
        {
            return ResultadoDeDominio.Rechazado("Ese dominio ya está dado de alta en la plataforma.");
        }

        var nuevo = new DominioDeTenant
        {
            TenantId = tenantId,
            Dominio = normalizado,
            TokenDeVerificacion = NuevoToken(),
        };

        _db.Dominios.Add(nuevo);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ResultadoDeDominio.Ok(ADto(nuevo));
    }

    public async Task<ResultadoDeDominio?> VerificarAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var dominio = await _db.Dominios
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (dominio is null)
        {
            return null;
        }

        await RevisarAsync(dominio, cancellationToken).ConfigureAwait(false);

        // El primero que verifica se vuelve el canónico solo. Un dominio verificado que
        // sirve tráfico pero no genera URL canónica es un estado que no le sirve a nadie.
        if (dominio.Estado == EstadoDeDominio.Verificado
            && !await _db.Dominios.AnyAsync(d => d.EsPrincipal, cancellationToken).ConfigureAwait(false))
        {
            dominio.EsPrincipal = true;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ResultadoDeDominio.Ok(ADto(dominio));
    }

    public async Task<ResultadoDeDominio?> MarcarPrincipalAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var dominios = await _db.Dominios.ToListAsync(cancellationToken).ConfigureAwait(false);
        var elegido = dominios.Find(d => d.Id == id);

        if (elegido is null)
        {
            return null;
        }

        if (elegido.Estado != EstadoDeDominio.Verificado)
        {
            return ResultadoDeDominio.Rechazado(
                "Solo un dominio verificado puede ser el principal: es el que va en las URLs "
                + "que indexa Google.");
        }

        foreach (var dominio in dominios)
        {
            dominio.EsPrincipal = dominio.Id == id;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ResultadoDeDominio.Ok(ADto(elegido));
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        var dominio = await _db.Dominios
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (dominio is null)
        {
            return false;
        }

        _db.Dominios.Remove(dominio);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Repasa los dominios de todas las automotoras: los pendientes por si ya se propagó el
    /// DNS, y los verificados por si dejaron de apuntar.
    /// </summary>
    /// <remarks>
    /// Corre sin tenant resuelto —lo dispara el cron, no un usuario—, así que necesita el
    /// escape cross-tenant para leer y para escribir. Es el precio de que el alta de un
    /// dominio termine sola en vez de quedar esperando a que alguien apriete un botón.
    /// </remarks>
    public async Task<ResumenDeVerificaciones> ReverificarPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var corte = ahora.AddDays(-_opciones.DiasEntreReverificaciones);

        var pendientes = await _db.Dominios
            .IgnoreQueryFilters()
            .Where(d => d.UltimaVerificacion == null
                        || d.Estado != EstadoDeDominio.Verificado
                        || d.UltimaVerificacion < corte)
            .OrderBy(d => d.UltimaVerificacion ?? DateTime.MinValue)
            .Take(_opciones.MaximoPorCorrida)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var verificados = 0;
        var fallidos = 0;
        var caidos = 0;

        foreach (var dominio in pendientes)
        {
            var antes = dominio.Estado;

            await RevisarAsync(dominio, cancellationToken).ConfigureAwait(false);

            if (dominio.Estado == EstadoDeDominio.Verificado)
            {
                verificados++;
            }
            else
            {
                fallidos++;
            }

            if (dominio.Estado == EstadoDeDominio.Caido && antes != EstadoDeDominio.Caido)
            {
                caidos++;
            }
        }

        using (var _ = _db.PermitirEscrituraCrossTenant())
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ResumenDeVerificaciones(pendientes.Count, verificados, fallidos, caidos);
    }

    /// <summary>
    /// Consulta el DNS y deja la entidad con el estado que corresponda. No guarda: quien
    /// llama decide cuándo, porque el cron guarda una sola vez al final.
    /// </summary>
    private async Task RevisarAsync(DominioDeTenant dominio, CancellationToken cancellationToken)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        dominio.UltimaVerificacion = ahora;

        IReadOnlyList<string> valores;

        try
        {
            valores = await _dns
                .TxtAsync(NombresDeDominio.NombreDelTxt(dominio.Dominio), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ConsultaDnsFallidaException)
        {
            // Un DNS que no contesta no prueba nada. No cuenta como fallo, porque si contara
            // una caída del resolver apagaría los sitios de todas las automotoras a la vez.
            dominio.UltimoError = ErrorDeConsulta;
            return;
        }

        if (valores.Any(valor => string.Equals(valor.Trim(), dominio.TokenDeVerificacion, StringComparison.Ordinal)))
        {
            dominio.Estado = EstadoDeDominio.Verificado;
            dominio.VerificadoEn ??= ahora;
            dominio.VerificacionesFallidas = 0;
            dominio.UltimoError = null;
            return;
        }

        dominio.VerificacionesFallidas++;
        dominio.UltimoError = valores.Count > 0 ? ErrorTxtDistinto : ErrorSinTxt;

        if (dominio.Estado == EstadoDeDominio.Verificado
            && dominio.VerificacionesFallidas >= _opciones.ToleranciaDeFallos)
        {
            dominio.Estado = EstadoDeDominio.Caido;

            // Un dominio caído no puede seguir siendo el canónico: quedaría indexando URLs
            // que ya no resuelven.
            dominio.EsPrincipal = false;
        }
    }

    private DominioDto ADto(DominioDeTenant dominio) => new(
        dominio.Id,
        dominio.Dominio,
        dominio.Estado.ToString(),
        dominio.EsPrincipal,
        dominio.VerificadoEn,
        dominio.UltimaVerificacion,
        dominio.UltimoError,
        new RegistroDnsDto(
            "TXT",
            NombresDeDominio.NombreDelTxt(dominio.Dominio),
            dominio.TokenDeVerificacion,
            "Prueba que el dominio es tuyo. Podés borrarlo una vez que quede verificado, "
            + "aunque conviene dejarlo: si lo sacás, la revisión periódica lo va a dar por caído."),
        Instrucciones(dominio.Dominio));

    /// <summary>
    /// Adónde apuntar el tráfico. Vacío si la plataforma no configuró su destino.
    /// </summary>
    private IReadOnlyList<RegistroDnsDto> Instrucciones(string dominio)
    {
        var registros = new List<RegistroDnsDto>();

        // El apex de un dominio no admite CNAME por el RFC, así que se dan las dos formas y
        // cada automotora usa la que le corresponde según lo que haya dado de alta.
        var esApex = dominio.Count(c => c == '.') <= 1;

        if (esApex && !string.IsNullOrWhiteSpace(_opciones.DestinoIp))
        {
            registros.Add(new RegistroDnsDto(
                "A",
                dominio,
                _opciones.DestinoIp,
                "Manda el tráfico del dominio a nuestros servidores. El apex no admite CNAME, por eso va un A."));
        }

        if (!esApex && !string.IsNullOrWhiteSpace(_opciones.DestinoCname))
        {
            registros.Add(new RegistroDnsDto(
                "CNAME",
                dominio,
                _opciones.DestinoCname,
                "Manda el tráfico del subdominio a nuestros servidores."));
        }

        return registros;
    }

    private int TenantDelRequest()
        => _tenantContext.TenantId
           ?? throw new InvalidOperationException(
               "No hay tenant resuelto: no se sabe de quién sería el dominio.");

    /// <summary>
    /// Token aleatorio en hexadecimal. Va en un TXT público, así que no es un secreto que
    /// proteja nada: solo tiene que ser imposible de adivinar por quien no lo vio.
    /// </summary>
    private static string NuevoToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
