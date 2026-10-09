using AutomotoraSaaS.Api.Configuracion;
using Microsoft.Extensions.Configuration;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Lo que impide arrancar en producción y lo que solo se avisa. El caso que importa es el
/// de la configuración por defecto: tiene que frenar, no levantar con fotos en el disco.
/// </summary>
public sealed class VerificacionDeProduccionTests
{
    private static readonly Dictionary<string, string?> Completa = new()
    {
        ["ConnectionStrings:Default"] = "Server=db;Database=automotora_saas;User Id=app;Password=x;",
        ["Jwt:Issuer"] = "automotora-saas",
        ["Jwt:Audience"] = "automotora-saas-clients",
        ["Jwt:Secret"] = new string('s', 40),
        ["Storage:Provider"] = "R2",
        ["Storage:PublicBaseUrl"] = "https://fotos.ejemplo.uy",
        ["Storage:Bucket"] = "automotora-saas",
        ["Storage:Endpoint"] = "https://cuenta.r2.cloudflarestorage.com",
        ["Storage:AccessKeyId"] = "id",
        ["Storage:SecretAccessKey"] = "clave",
        ["Jobs:Secret"] = "secreto-de-jobs",
        ["Correo:Host"] = "smtp.ejemplo.uy",
        ["Correo:Remitente"] = "avisos@ejemplo.uy",
        ["Deploy:IpsPublicas:0"] = "203.0.113.10",
        ["Sentry:Dsn"] = "https://clave@sentry.example/1",
    };

    [Fact]
    public void Con_todo_completo_no_hay_errores_ni_avisos()
    {
        var resultado = VerificacionDeProduccion.Revisar(Configuracion(Completa));

        Assert.Empty(resultado.Errores);
        Assert.Empty(resultado.Avisos);
    }

    [Fact]
    public void El_appsettings_json_tal_cual_no_arranca_y_dice_todo_lo_que_falta()
    {
        // Lo que trae appsettings.json: storage local con URL de localhost y secretos vacíos.
        var resultado = VerificacionDeProduccion.Revisar(Configuracion(new()
        {
            ["Jwt:Issuer"] = "automotora-saas",
            ["Jwt:Audience"] = "automotora-saas-clients",
            ["Storage:Provider"] = "Local",
            ["Storage:PublicBaseUrl"] = "http://localhost:5080/uploads",
        }));

        Assert.Contains(resultado.Errores, e => e.Contains("ConnectionStrings:Default", StringComparison.Ordinal));
        Assert.Contains(resultado.Errores, e => e.Contains("Jwt:Secret", StringComparison.Ordinal));
        Assert.Contains(resultado.Errores, e => e.Contains("\"Local\"", StringComparison.Ordinal));
        Assert.Contains(resultado.Errores, e => e.Contains("PublicBaseUrl", StringComparison.Ordinal));
        Assert.Contains(resultado.Errores, e => e.Contains("Jobs:Secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("http://fotos.ejemplo.uy")]
    [InlineData("https://localhost/uploads")]
    [InlineData("fotos.ejemplo.uy")]
    public void La_url_de_las_fotos_tiene_que_ser_https_y_publica(string url)
    {
        var resultado = VerificacionDeProduccion.Revisar(Configuracion(Con("Storage:PublicBaseUrl", url)));

        Assert.Single(resultado.Errores);
    }

    [Fact]
    public void Un_secreto_de_jwt_corto_no_arranca()
    {
        var resultado = VerificacionDeProduccion.Revisar(Configuracion(Con("Jwt:Secret", "corto")));

        Assert.Single(resultado.Errores);
    }

    [Fact]
    public void Sin_correo_ni_ips_ni_sentry_arranca_pero_avisa()
    {
        var config = new Dictionary<string, string?>(Completa)
        {
            ["Correo:Host"] = "",
            ["Deploy:IpsPublicas:0"] = "CAMBIAR-por-la-IP-del-hosting",
            ["Sentry:Dsn"] = "",
        };

        var resultado = VerificacionDeProduccion.Revisar(Configuracion(config));

        Assert.Empty(resultado.Errores);
        Assert.Equal(3, resultado.Avisos.Count);
    }

    private static Dictionary<string, string?> Con(string clave, string valor)
        => new(Completa) { [clave] = valor };

    private static IConfiguration Configuracion(Dictionary<string, string?> valores)
        => new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
}
