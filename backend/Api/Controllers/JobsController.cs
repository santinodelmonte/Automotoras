using System.Security.Cryptography;
using System.Text;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
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

    public JobsController(AppDbContext db, IConfiguration configuracion)
    {
        _db = db;
        _configuracion = configuracion;
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
