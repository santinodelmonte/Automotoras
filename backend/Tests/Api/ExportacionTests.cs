using System.IO.Compression;
using System.Net;
using System.Text;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Exportacion;
using AutomotoraSaaS.Core.Planes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// La exportación que promete la propuesta: completa, solo del tenant que la pide y
/// disponible también con el sitio suspendido.
/// </summary>
public sealed class ExportacionTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public ExportacionTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_zip_trae_todos_los_archivos_prometidos()
    {
        var archivos = await ExportarAsync(FabricaDeApi.EmailOwnerNorte);

        Assert.Equal(
            ["LEEME.txt", "busquedas_sin_resultado.csv", "consultas_por_dia.csv", "eventos_por_dia.csv", "fotos.csv", "vehiculos.csv"],
            archivos.Keys.Order(StringComparer.Ordinal));

        Assert.StartsWith("id,marca,modelo", archivos["vehiculos.csv"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_owner_exporta_unicamente_los_datos_de_su_automotora()
    {
        _api.ConLaBase(db =>
        {
            var ayer = DateTime.UtcNow.AddDays(-1);
            db.Eventos.Add(FabricaDeApi.Evento(_api.TenantNorte, _api.VehiculoDeNorte, TipoEvento.ClickWhatsapp, ayer));
            db.Eventos.Add(FabricaDeApi.Evento(_api.TenantSur, _api.VehiculoDeSur, TipoEvento.ClickWhatsapp, ayer));
        });

        var archivos = await ExportarAsync(FabricaDeApi.EmailOwnerNorte);

        var vehiculos = Ids(archivos["vehiculos.csv"], columna: 0);
        Assert.Contains(_api.VehiculoDeNorte, vehiculos);
        Assert.Contains(_api.VendidoDeNorte, vehiculos);
        Assert.DoesNotContain(_api.VehiculoDeSur, vehiculos);

        var consultas = Ids(archivos["consultas_por_dia.csv"], columna: 1);
        Assert.Contains(_api.VehiculoDeNorte, consultas);
        Assert.DoesNotContain(_api.VehiculoDeSur, consultas);
    }

    [Fact]
    public async Task Los_eventos_salen_agregados_sin_datos_de_los_visitantes()
    {
        var archivos = await ExportarAsync(FabricaDeApi.EmailOwnerNorte);

        var encabezado = archivos["eventos_por_dia.csv"].Split("\r\n")[0];

        Assert.Equal("fecha,vehiculo_id,tipo,cantidad", encabezado);
        Assert.All(archivos.Values, contenido =>
        {
            Assert.DoesNotContain("ip_hash", contenido, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("session", contenido, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Un_vendedor_no_puede_exportar()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/tenant/exportacion");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>Es exactamente la situación que la promesa contempla.</summary>
    [Fact]
    public async Task Una_automotora_suspendida_puede_exportar()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, owner) = _api.AutomotoraConPlan("exporta-suspendida", CodigosDePlan.Demanda, hoy.AddDays(-60));

        var archivos = await ExportarAsync(owner);

        Assert.Contains("vehiculos.csv", archivos.Keys);
    }

    [Fact]
    public void El_csv_escapa_comas_comillas_y_neutraliza_formulas()
    {
        var csv = new Csv("a", "b", "c", "d")
            .Fila("con, coma", "con \"comillas\"", "=HYPERLINK(\"x\")", -5.5m)
            .ToString();

        var fila = csv.Split("\r\n")[1];

        Assert.Equal("\"con, coma\",\"con \"\"comillas\"\"\",\"'=HYPERLINK(\"\"x\"\")\",-5.5", fila);
    }

    private async Task<Dictionary<string, string>> ExportarAsync(string email)
    {
        using var cliente = await _api.ClienteDeAsync(email);

        var respuesta = await cliente.GetAsync("/api/tenant/exportacion");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/zip", respuesta.Content.Headers.ContentType?.MediaType);

        using var zip = new ZipArchive(await respuesta.Content.ReadAsStreamAsync(), ZipArchiveMode.Read);

        var archivos = new Dictionary<string, string>();

        foreach (var entrada in zip.Entries)
        {
            using var lector = new StreamReader(entrada.Open(), Encoding.UTF8);
            archivos[entrada.FullName] = await lector.ReadToEndAsync();
        }

        return archivos;
    }

    private static List<int> Ids(string csv, int columna)
        => csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(fila => int.Parse(fila.Split(',')[columna], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
}
