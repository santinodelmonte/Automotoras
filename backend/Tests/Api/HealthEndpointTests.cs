using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Confirma que el pipeline de tests de integración funciona antes de que haya
/// features que testear.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_responde_200_con_estado_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthStatusDto>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
        Assert.NotEqual(default, body.Timestamp);
    }
}
