using System.Net;
using AutomotoraSaaS.Core.Publico;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Las páginas del frontend servidas por la API, con las etiquetas que leen WhatsApp y los
/// buscadores ya puestas en el HTML.
/// </summary>
public sealed class SitioDelFrontendTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public SitioDelFrontendTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task La_ficha_de_un_vehiculo_sale_con_su_titulo_y_su_descripcion()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/t/norte/vehiculos/{_api.VehiculoDeNorte}");
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("text/html", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>Volkswagen Gol", html, StringComparison.Ordinal);
        Assert.Contains("property=\"og:title\" content=\"Volkswagen Gol", html, StringComparison.Ordinal);
        Assert.Contains("name=\"description\"", html, StringComparison.Ordinal);

        // Norte tiene su dominio verificado: la URL canónica es la de su dominio, no la del slug.
        Assert.Contains(
            $"href=\"https://{FabricaDeApi.DominioDeNorte}/vehiculos/{_api.VehiculoDeNorte}\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Automotora SaaS", html, StringComparison.Ordinal);
    }

    /// <summary>Vendido: la página dice que ya no está, pero responde 404 para salir del índice de Google.</summary>
    [Fact]
    public async Task Un_vehiculo_vendido_responde_404_y_no_se_indexa()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/t/norte/vehiculos/{_api.VendidoDeNorte}");
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
    }

    /// <summary>El vehículo de otra automotora no se describe bajo la dirección de esta.</summary>
    [Fact]
    public async Task Un_vehiculo_de_otra_automotora_no_aparece()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/t/norte/vehiculos/{_api.VehiculoDeSur}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task La_home_sale_con_el_nombre_de_la_automotora()
    {
        using var cliente = _api.CreateClient();

        var html = await cliente.GetStringAsync("/t/norte");

        Assert.Contains("<title>Automotora Norte — Autos en venta</title>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_automotora_que_no_existe_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>Una ruta de la API que no existe no puede devolver la home con 200.</summary>
    [Fact]
    public async Task Una_ruta_inexistente_de_la_api_sigue_siendo_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.NotEqual("text/html", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task El_panel_no_se_indexa()
    {
        using var cliente = _api.CreateClient();

        var html = await cliente.GetStringAsync("/admin/vehiculos");

        Assert.Contains("noindex", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_robots_de_una_automotora_apunta_a_su_sitemap()
    {
        using var cliente = _api.CreateClient();

        var texto = await cliente.GetStringAsync("/t/norte/robots.txt");

        Assert.Contains("/t/norte/api/public/sitemap.xml", texto, StringComparison.Ordinal);
        Assert.Contains("Disallow: /admin", texto, StringComparison.Ordinal);
    }

    /// <summary>El nombre y la descripción los escribe el cliente: sin escapar serían un XSS en cada visita.</summary>
    [Fact]
    public void Lo_que_escribe_el_cliente_sale_escapado()
    {
        var html = PaginaDelSitio.Renderizar(
            "<head><title>x</title></head>",
            new MetaDelSitio("\"><script>alert(1)</script>", "<b>"));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }
}
