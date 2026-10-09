using System.Net;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Los headers de seguridad salen en todo: en la API, en las páginas del sitio y en los
/// errores, que es justo donde una política puesta por controller se olvida.
/// </summary>
public sealed class HeadersDeSeguridadTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public HeadersDeSeguridadTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Theory]
    [InlineData("/api/health")]
    [InlineData("/t/norte/")]
    [InlineData("/api/no-existe")]
    [InlineData("/admin")]
    public async Task Toda_respuesta_sale_con_los_headers_de_seguridad(string ruta)
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal("nosniff", Valor(respuesta, "X-Content-Type-Options"));
        Assert.Equal("DENY", Valor(respuesta, "X-Frame-Options"));
        Assert.Equal("strict-origin-when-cross-origin", Valor(respuesta, "Referrer-Policy"));

        var politica = Valor(respuesta, "Content-Security-Policy");
        Assert.Contains("script-src 'self'", politica, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", politica, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", politica, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ningún script inline ni de otro origen: si alguien agrega uno al index.html, el
    /// navegador lo bloquea y este test explica por qué.
    /// </summary>
    [Fact]
    public async Task La_politica_no_habilita_scripts_inline_ni_eval()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/");
        var scripts = Valor(respuesta, "Content-Security-Policy")
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(directiva => directiva.StartsWith("script-src", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("script-src 'self'", scripts);
    }

    private static string Valor(HttpResponseMessage respuesta, string header)
        => respuesta.Headers.TryGetValues(header, out var valores) || respuesta.Content.Headers.TryGetValues(header, out valores)
            ? string.Join(", ", valores)
            : string.Empty;
}
