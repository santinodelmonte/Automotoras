using System.Net;
using System.Net.Http.Json;
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

    /// <summary>Un link roto no puede quedar indexado como una copia de la portada.</summary>
    [Fact]
    public async Task Una_direccion_que_el_sitio_no_tiene_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/no-existe");
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
        Assert.Contains("no encontrada — ", System.Net.WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/t/norte/")]
    [InlineData("/t/norte/vehiculos")]
    [InlineData("/t/norte/vehiculos/")]
    [InlineData("/t/norte/privacidad")]
    public async Task Las_paginas_del_sitio_responden_200(string ruta)
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>La ficha lleva los datos estructurados que lee Google: precio, año, kilómetros.</summary>
    [Fact]
    public async Task La_ficha_lleva_los_datos_estructurados_del_vehiculo()
    {
        using var cliente = _api.CreateClient();

        var html = await cliente.GetStringAsync($"/t/norte/vehiculos/{_api.VehiculoDeNorte}");

        var inicio = html.IndexOf("<script type=\"application/ld+json\">", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "La ficha no tiene JSON-LD.");

        var contenido = html[(html.IndexOf('>', inicio) + 1)..html.IndexOf("</script>", inicio, StringComparison.Ordinal)];
        using var json = System.Text.Json.JsonDocument.Parse(contenido);
        var raiz = json.RootElement;

        Assert.Equal("Car", raiz.GetProperty("@type").GetString());
        Assert.Equal("Volkswagen", raiz.GetProperty("brand").GetProperty("name").GetString());
        Assert.Equal("Offer", raiz.GetProperty("offers").GetProperty("@type").GetString());
        Assert.True(raiz.GetProperty("offers").GetProperty("price").GetDecimal() > 0);
    }

    /// <summary>Un "&lt;/script&gt;" en el nombre de la automotora no puede cerrar el bloque de JSON-LD.</summary>
    [Fact]
    public void Los_datos_estructurados_no_se_pueden_cerrar_desde_adentro()
    {
        var meta = PaginaDelSitio.DeAutomotora("</script><script>alert(1)</script>", null, 3, null, "https://x.uy");

        var html = PaginaDelSitio.Renderizar("<head><title>x</title></head>", meta);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "</script>"));
        Assert.DoesNotContain("<script>alert", html, StringComparison.Ordinal);
    }

    /// <summary>Lo que no se indexa tampoco describe nada para Google.</summary>
    [Fact]
    public async Task Un_vehiculo_vendido_no_lleva_datos_estructurados()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/t/norte/vehiculos/{_api.VendidoDeNorte}");
        var html = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("application/ld+json", html, StringComparison.Ordinal);
    }

    /// <summary>Los errores del navegador se aceptan sin sesión: el comprador no tiene cuenta.</summary>
    [Fact]
    public async Task Un_error_del_navegador_se_acepta_sin_sesion()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/errores-del-cliente",
            new { mensaje = "TypeError: x is undefined", pila = "at Ficha", ruta = "/vehiculos/1" });

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
    }
}
