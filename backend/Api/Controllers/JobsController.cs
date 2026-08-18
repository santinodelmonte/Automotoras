using System.Security.Cryptography;
using System.Text;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Dominios;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Trabajos periódicos, disparados por un cron externo.
/// </summary>
/// <remarks>
/// No hay <c>BackgroundService</c> ni <c>IHostedService</c> en ningún lado, y no es una
/// omisión: el deploy es shared hosting Windows/IIS, donde el app pool recicla cuando
/// quiere. Un job crítico adentro del proceso web se corta a mitad de camino sin que nadie
/// se entere. Como endpoint, el cron externo tiene reintentos, registro y una respuesta
/// HTTP que dice si salió bien.
/// <para>
/// Se autentica con el header <c>X-Job-Secret</c> y no con JWT: el cron no es un usuario,
/// no tiene sesión y no debería poder hacer nada más que esto.
/// </para>
/// </remarks>
[ApiController]
[Route("api/jobs")]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class JobsController : ControllerBase
{
    private const string HeaderDelSecreto = "X-Job-Secret";

    private readonly AppDbContext _db;
    private readonly IConfiguration _configuracion;
    private readonly IServicioDeDominios _dominios;

    public JobsController(AppDbContext db, IConfiguration configuracion, IServicioDeDominios dominios)
    {
        _db = db;
        _configuracion = configuracion;
        _dominios = dominios;
    }

    /// <summary>
    /// Registra la cotización del dólar del día. Idempotente: correrlo dos veces actualiza
    /// la fila en vez de duplicarla, que es lo que hace que el cron pueda reintentar.
    /// </summary>
    [HttpPost("cotizaciones")]
    [ProducesResponseType(typeof(CotizacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CotizacionDto>> Cotizaciones(
        RegistrarCotizacionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var cotizacion = await _db.Cotizaciones
            .FirstOrDefaultAsync(c => c.Fecha == request.Fecha, cancellationToken)
            .ConfigureAwait(false);

        if (cotizacion is null)
        {
            cotizacion = new Cotizacion { Fecha = request.Fecha };
            _db.Cotizaciones.Add(cotizacion);
        }

        cotizacion.UsdUyu = request.UsdUyu;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(new CotizacionDto(cotizacion.Fecha, cotizacion.UsdUyu));
    }

    /// <summary>
    /// Guarda los precios de mercado relevados por el cron.
    /// </summary>
    /// <remarks>
    /// Idempotente por modelo, año, moneda, fecha y fuente: correrlo dos veces el mismo día
    /// actualiza la fila en vez de duplicar la serie. Es lo que permite reintentar sin
    /// arruinar el histórico, que es justamente lo que le da valor al dato.
    /// </remarks>
    [HttpPost("precios-referencia")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> PreciosDeReferencia(
        RegistrarPreciosReferenciaRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        var modelos = request.Precios.Select(p => p.ModeloId).Distinct().ToList();

        var existentes = await _db.Modelos
            .Where(m => modelos.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var faltantes = modelos.Except(existentes).ToList();

        if (faltantes.Count > 0)
        {
            return Problem(
                detail: $"Estos modelos no existen: {string.Join(", ", faltantes)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var fechas = request.Precios.Select(p => p.Fecha).Distinct().ToList();

        var yaGuardados = await _db.PreciosReferencia
            .Where(p => modelos.Contains(p.ModeloId) && fechas.Contains(p.Fecha))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var indice = yaGuardados.ToDictionary(
            p => (p.ModeloId, p.Anio, p.Moneda, p.Fecha, p.Fuente));

        foreach (var relevado in request.Precios)
        {
            var moneda = Enumeraciones.Parsear<Moneda>(relevado.Moneda);
            var fuente = relevado.Fuente.Trim();
            var clave = (relevado.ModeloId, relevado.Anio, moneda, relevado.Fecha, fuente);

            if (!indice.TryGetValue(clave, out var precio))
            {
                precio = new PrecioReferencia
                {
                    ModeloId = relevado.ModeloId,
                    Anio = relevado.Anio,
                    Moneda = moneda,
                    Fecha = relevado.Fecha,
                    Fuente = fuente,
                };

                _db.PreciosReferencia.Add(precio);
                indice[clave] = precio;
            }

            precio.Promedio = relevado.Promedio;
            precio.Minimo = relevado.Minimo;
            precio.Maximo = relevado.Maximo;
            precio.Muestras = relevado.Muestras;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Ok(request.Precios.Count);
    }

    /// <summary>
    /// Repasa los dominios propios: los pendientes por si ya propagó el DNS, y los
    /// verificados por si dejaron de apuntar.
    /// </summary>
    /// <remarks>
    /// Es lo que hace que el alta de un dominio termine sola. Sin esto, una automotora que
    /// publica el TXT un domingo a la noche queda esperando hasta que alguien entre al panel
    /// y apriete verificar.
    /// <para>
    /// Idempotente: correrlo dos veces seguidas vuelve a consultar el DNS y llega al mismo
    /// estado. Un dominio que ya verificó no se toca hasta que pase la ventana de
    /// reverificación, así que reintentar no castiga a nadie.
    /// </para>
    /// </remarks>
    [HttpPost("verificar-dominios")]
    [ProducesResponseType(typeof(ResumenDeVerificaciones), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResumenDeVerificaciones>> VerificarDominios(
        CancellationToken cancellationToken)
    {
        if (!SecretoCorrecto())
        {
            return Unauthorized();
        }

        return Ok(await _dominios.ReverificarPendientesAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Compara el header contra el secreto configurado, en tiempo constante.
    /// </summary>
    /// <remarks>
    /// Un <c>==</c> de strings corta en el primer carácter distinto, y esa diferencia de
    /// tiempo se puede medir para adivinar el secreto de a un carácter. Es un endpoint
    /// público: alguien lo va a probar.
    /// </remarks>
    private bool SecretoCorrecto()
    {
        var esperado = _configuracion["Jobs:Secret"];

        // Sin secreto configurado no se ejecuta ningún job. Fallar cerrado: un secreto
        // vacío que matchee un header vacío deja los jobs abiertos a cualquiera.
        if (string.IsNullOrWhiteSpace(esperado))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(HeaderDelSecreto, out var recibido))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recibido.ToString()),
            Encoding.UTF8.GetBytes(esperado));
    }
}
