using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Tenants;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Resolución del tenant en el sitio público: por dominio propio o por el slug de
/// <c>/t/{slug}</c> en desarrollo, siempre validado contra la tabla <c>tenants</c>.
/// </summary>
public sealed class SitioPublicoTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public SitioPublicoTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_slug_de_la_ruta_resuelve_la_automotora()
    {
        using var cliente = _api.CreateClient();

        var tenant = await cliente.GetFromJsonAsync<TenantPublicoDto>("/t/norte/api/public/tenant");

        Assert.NotNull(tenant);
        Assert.Equal("norte", tenant.Slug);
        Assert.Equal("Automotora Norte", tenant.Nombre);
        Assert.Equal("#059669", tenant.ColorPrimario);
    }

    [Fact]
    public async Task El_dominio_propio_resuelve_la_automotora()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Host = FabricaDeApi.DominioDeNorte;

        var tenant = await cliente.GetFromJsonAsync<TenantPublicoDto>("/api/public/tenant");

        Assert.NotNull(tenant);
        Assert.Equal("norte", tenant.Slug);
    }

    [Fact]
    public async Task El_dominio_con_www_resuelve_igual()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Host = "www." + FabricaDeApi.DominioDeNorte;

        var respuesta = await cliente.GetAsync("/api/public/tenant");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_slug_que_no_existe_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/no-existe/api/public/tenant");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// Dar de baja una automotora tiene que apagarle el sitio, no dejarlo publicado.
    /// </summary>
    [Fact]
    public async Task Una_automotora_dada_de_baja_no_tiene_sitio()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/apagada/api/public/tenant");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// Sin dominio conocido y sin slug no hay tenant. No existe el caso "automotora por
    /// defecto": sería servirle a un visitante el stock de cualquiera.
    /// </summary>
    [Fact]
    public async Task Sin_dominio_ni_slug_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/public/tenant");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// El sitio público resuelve el tenant solo, sin que el visitante lo elija. Un
    /// endpoint público que aceptara el tenant como parámetro sería un catálogo abierto de
    /// todos los clientes del SaaS.
    /// </summary>
    [Fact]
    public async Task El_endpoint_publico_no_acepta_el_tenant_como_parametro()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/api/public/tenant?tenantId={_api.TenantNorte}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
