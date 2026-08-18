using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Health;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El health check sigue siendo público y sigue respondiendo con el pipeline completo
/// montado: autenticación, resolución de tenant y autorización en el medio.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public HealthEndpointTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Health_responde_200_con_estado_ok()
    {
        using var cliente = _api.CreateClient();

        var response = await cliente.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthStatusDto>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.NotEqual(default, body.Timestamp);
    }

    /// <summary>
    /// Sin tenant resuelto y sin token, el health sigue andando: no es un endpoint de
    /// ningún tenant.
    /// </summary>
    [Fact]
    public async Task Health_no_necesita_tenant_ni_token()
    {
        using var cliente = _api.CreateClient();

        var response = await cliente.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
