using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Reportes de demanda: qué se mira, qué se consulta, qué se busca sin encontrar y qué
/// conviene comprar.
/// </summary>
/// <remarks>
/// Este es el producto. El catálogo lo tiene cualquiera; lo que no tiene nadie es la
/// respuesta a "qué conviene comprar", y sale de cruzar lo que la gente miró con lo que
/// preguntó y con lo que buscó y no estaba.
/// <para>
/// Solo el Owner. El vendedor carga vehículos y atiende consultas; los reportes son del
/// dueño.
/// </para>
/// </remarks>
[ApiController]
[Route("api/reportes")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ReportesController : ControllerBase
{
    private const int DiasPorDefecto = 30;
    private const int DiasMaximos = 365;

    private readonly IServicioDeReportes _reportes;

    public ReportesController(IServicioDeReportes reportes)
    {
        _reportes = reportes;
    }

    /// <param name="dias">Ventana de análisis. Por defecto 30, tope un año.</param>
    [HttpGet("demanda")]
    [ProducesResponseType(typeof(ReporteDeDemandaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ReporteDeDemandaDto>> Demanda(
        [FromQuery] int dias,
        CancellationToken cancellationToken)
        => Ok(await _reportes.DemandaAsync(Ventana(dias), cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Qué conviene traer, cruzando la demanda insatisfecha con la rotación histórica de
    /// la propia automotora.
    /// </summary>
    [HttpGet("sugerencias")]
    [ProducesResponseType(typeof(IReadOnlyList<SugerenciaDeCompraDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SugerenciaDeCompraDto>>> Sugerencias(
        [FromQuery] int dias,
        CancellationToken cancellationToken)
        => Ok(await _reportes.SugerenciasDeCompraAsync(Ventana(dias), cancellationToken).ConfigureAwait(false));

    private static int Ventana(int dias)
        => Math.Clamp(dias <= 0 ? DiasPorDefecto : dias, 1, DiasMaximos);
}
