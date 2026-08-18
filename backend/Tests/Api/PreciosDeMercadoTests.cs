using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Reportes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El job de precios de referencia y su llegada al reporte de demanda.
/// </summary>
/// <remarks>
/// El precio de mercado es global: un Corolla 2018 vale lo mismo mirado desde cualquier
/// automotora. Lo que sí es de cada tenant es contra qué se compara, y eso es lo que se
/// prueba acá.
/// </remarks>
public sealed class PreciosDeMercadoTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public PreciosDeMercadoTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Sin_el_secreto_el_job_no_corre()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/jobs/precios-de-mercado", Lote(15_000m));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// El cron reintenta. Correr el mismo lote dos veces tiene que actualizar la fila del
    /// día y no dejar dos precios distintos para el mismo modelo, año y fecha.
    /// </summary>
    [Fact]
    public async Task El_lote_es_idempotente_y_el_reintento_actualiza()
    {
        using var cliente = ClienteDeJob();

        // Con fuente propia: la clave única incluye la fuente, así que este par de lotes
        // no se pisa con los que cargan los demás tests sobre el mismo modelo y año.
        var primera = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-de-mercado", Lote(15_000m, fuente: "FuenteDeEsteTest"));

        var segunda = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-de-mercado", Lote(16_000m, fuente: "FuenteDeEsteTest"));

        var uno = await primera.Content.ReadFromJsonAsync<ResultadoDePreciosDto>();
        var dos = await segunda.Content.ReadFromJsonAsync<ResultadoDePreciosDto>();

        Assert.NotNull(uno);
        Assert.NotNull(dos);
        Assert.Equal(1, uno.Guardados);
        Assert.Equal(0, uno.Actualizados);
        Assert.Equal(0, dos.Guardados);
        Assert.Equal(1, dos.Actualizados);
    }

    /// <summary>
    /// Con dos avisos, la mediana es el precio que puso una persona. Un precio de
    /// referencia equivocado es peor que ninguno: el que falta se nota, el que está mal se
    /// cree.
    /// </summary>
    [Fact]
    public async Task Un_snapshot_con_muy_pocas_publicaciones_se_rechaza()
    {
        using var cliente = ClienteDeJob();

        var lote = Lote(15_000m) with
        {
            Precios =
            [
                new SnapshotDePrecioRequest(_api.ModeloId, 2019, "Usd", 15_000m, 12_000m, 18_000m, Publicaciones: 2),
            ],
        };

        var respuesta = await cliente.PostAsJsonAsync("/api/jobs/precios-de-mercado", lote);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Un modelo que el snapshot nombra y el catálogo no tiene no puede voltear el lote:
    /// quien lo arma trabaja contra una copia que puede estar un día atrasada.
    /// </summary>
    [Fact]
    public async Task Un_modelo_desconocido_no_voltea_el_lote()
    {
        using var cliente = ClienteDeJob();

        var lote = Lote(15_000m) with
        {
            Precios =
            [
                new SnapshotDePrecioRequest(999_999, 2019, "Usd", 15_000m, 12_000m, 18_000m, 40),
                new SnapshotDePrecioRequest(_api.ModeloId, 2013, "Usd", 7_000m, 5_000m, 9_000m, 25),
            ],
        };

        var respuesta = await cliente.PostAsJsonAsync("/api/jobs/precios-de-mercado", lote);
        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoDePreciosDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(resultado);
        Assert.Equal(1, resultado.Guardados);
    }

    /// <summary>
    /// Lo que el reporte tiene que decir: este auto está tanto por ciento arriba de lo que
    /// pide el mercado. Es la cifra que convierte "lleva tres meses sin venderse" en algo
    /// que se puede corregir.
    /// </summary>
    [Fact]
    public async Task El_reporte_compara_el_precio_publicado_contra_el_de_mercado()
    {
        using var deJob = ClienteDeJob();

        // La unidad de Norte está publicada a 15.000 y el mercado pide 12.000: un 25 % arriba.
        var lote = Lote(12_000m);

        var guardado = await deJob.PostAsJsonAsync("/api/jobs/precios-de-mercado", lote);
        guardado.EnsureSuccessStatusCode();

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=30");

        Assert.NotNull(reporte);

        var fila = reporte.Vehiculos.Single(v => v.VehiculoId == _api.VehiculoDeNorte);

        Assert.Equal(12_000m, fila.PrecioDeMercado);
        Assert.Equal(25m, fila.DiferenciaConElMercado);
        Assert.NotNull(fila.PrecioDeMercadoAl);
    }

    /// <summary>
    /// Sin snapshot no hay comparación, y el reporte lo dice en null en vez de inventar un
    /// cero que se leería como "el mercado lo regala".
    /// </summary>
    [Fact]
    public async Task Sin_precio_de_referencia_la_comparacion_viaja_vacia()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=30");

        Assert.NotNull(reporte);

        // El olvidado es un 2013 y ningún test le carga precio de mercado.
        var fila = reporte.Vehiculos.Single(v => v.VehiculoId == _api.OlvidadoDeNorte);

        Assert.Null(fila.PrecioDeMercado);
        Assert.Null(fila.DiferenciaConElMercado);
    }

    private HttpClient ClienteDeJob()
    {
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        return cliente;
    }

    /// <summary>Un lote de un solo precio, para el modelo y el año de la unidad sembrada.</summary>
    private RegistrarPreciosDeMercadoRequest Lote(decimal mediano, string fuente = "MercadoLibre") => new(
        DateOnly.FromDateTime(DateTime.UtcNow),
        fuente,
        [new SnapshotDePrecioRequest(_api.ModeloId, 2019, "Usd", mediano, mediano - 3_000m, mediano + 3_000m, 42)]);
}
