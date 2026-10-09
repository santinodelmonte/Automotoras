using AutomotoraSaaS.Api.Auth;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutomotoraSaaS.Api.Controllers;

/// <summary>
/// El plan de la automotora visto por su dueño: qué tiene, cuánto usa y cómo está el pago.
/// </summary>
/// <remarks>
/// Es lo que alimenta los avisos del panel: el de vencimiento y el de cerca del tope. Que
/// el cliente se entere del techo cuando lo choca, o de la deuda cuando se le apaga el
/// sitio, es la peor forma de enterarse.
/// </remarks>
[ApiController]
[Route("api/tenant/plan")]
[Authorize(Policy = Politicas.SoloOwner)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PlanDelTenantController : ControllerBase
{
    private readonly IPoliticaDePlan _politica;

    public PlanDelTenantController(IPoliticaDePlan politica)
    {
        _politica = politica;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SituacionDelPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SituacionDelPlanDto>> Obtener(CancellationToken cancellationToken)
    {
        var tenantId = User.TenantIdDelToken()
                       ?? throw new InvalidOperationException("El panel del Owner requiere un tenant en el token.");

        var situacion = await _politica.SituacionAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return Ok(situacion.ADto());
    }
}
