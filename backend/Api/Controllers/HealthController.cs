using AutomotoraSaaS.Core.Health;
using Microsoft.AspNetCore.Mvc;

namespace AutomotoraSaaS.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly TimeProvider _timeProvider;

    public HealthController(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    public ActionResult<HealthStatusDto> Get()
        => Ok(new HealthStatusDto("ok", _timeProvider.GetUtcNow()));
}
