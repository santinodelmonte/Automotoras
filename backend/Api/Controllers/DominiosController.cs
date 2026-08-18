using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Dominios;
using AutomotoraSaaS.Core.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// Los dominios propios de la automotora: alta, verificación por DNS y baja.
/// </summary>
/// <remarks>
/// Solo el Owner. Cambiar el dominio cambia la dirección por la que el negocio está
/// publicado, y eso no es una tarea de quien carga vehículos.
/// <para>
/// El alta la hace la automotora sola: escribe su dominio, publica el TXT que le decimos y
/// aprieta verificar. Nadie de la plataforma toca nada, que es toda la diferencia entre
/// esto y cargar el dominio a mano en una tabla.
/// </para>
/// </remarks>
[ApiController]
[Route("api/dominios")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DominiosController : ControllerBase
{
    private readonly IServicioDeDominios _dominios;

    public DominiosController(IServicioDeDominios dominios)
    {
        _dominios = dominios;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DominioDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DominioDto>>> Listar(CancellationToken cancellationToken)
        => Ok(await _dominios.ListarAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Da de alta un dominio. Queda pendiente hasta que el DNS confirme que es de esta
    /// automotora.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DominioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DominioDto>> Agregar(
        AgregarDominioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var resultado = await _dominios
            .AgregarAsync(request.Dominio, cancellationToken)
            .ConfigureAwait(false);

        if (resultado.Dominio is null)
        {
            return Conflicto(resultado.Rechazo);
        }

        return CreatedAtAction(nameof(Listar), new { id = resultado.Dominio.Id }, resultado.Dominio);
    }

    /// <summary>
    /// Consulta el DNS ahora y actualiza el estado con lo que encuentre.
    /// </summary>
    /// <remarks>
    /// Responde 200 tanto si verificó como si no: que el TXT todavía no esté no es un error
    /// del pedido, es el resultado normal de apretar el botón mientras el DNS propaga. El
    /// estado y el motivo vienen en el cuerpo.
    /// </remarks>
    [HttpPost("{id:int}/verificar")]
    [ProducesResponseType(typeof(DominioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DominioDto>> Verificar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _dominios.VerificarAsync(id, cancellationToken).ConfigureAwait(false);

        if (resultado is null)
        {
            return NoExiste(id);
        }

        return resultado.Dominio is null ? Conflicto(resultado.Rechazo) : Ok(resultado.Dominio);
    }

    /// <summary>Elige el dominio que va en las URLs canónicas y en el sitemap.</summary>
    [HttpPost("{id:int}/principal")]
    [ProducesResponseType(typeof(DominioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DominioDto>> Principal(int id, CancellationToken cancellationToken)
    {
        var resultado = await _dominios.MarcarPrincipalAsync(id, cancellationToken).ConfigureAwait(false);

        if (resultado is null)
        {
            return NoExiste(id);
        }

        return resultado.Dominio is null ? Conflicto(resultado.Rechazo) : Ok(resultado.Dominio);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken)
    {
        var borrado = await _dominios.EliminarAsync(id, cancellationToken).ConfigureAwait(false);

        return borrado ? NoContent() : NoExiste(id);
    }

    private ActionResult Conflicto(string? detalle)
        => Problem(detail: detalle, statusCode: StatusCodes.Status409Conflict);

    private ActionResult NoExiste(int id)
        => Problem(detail: $"No existe el dominio {id}.", statusCode: StatusCodes.Status404NotFound);
}
