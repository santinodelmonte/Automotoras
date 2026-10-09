using System.Net.Http.Json;
using System.Text.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Reportes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Las sugerencias de compra: demanda que no se pudo atender, cruzada contra el patio.
/// </summary>
/// <remarks>
/// Es el reporte que más caro sale si se equivoca. Los demás describen lo que pasó; este
/// le dice al dueño en qué gastar miles de dólares, así que lo que se testea no es solo
/// que la cuenta dé, sino que la sugerencia cambie cuando cambia el stock.
/// </remarks>
public sealed class SugerenciasTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public SugerenciasTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// Tres visitas distintas buscaron un modelo del que no hay una sola unidad
    /// publicada. Eso es demanda que se fue entera, y es una compra.
    /// </summary>
    [Fact]
    public async Task Un_modelo_que_nadie_encontro_y_no_esta_en_stock_se_sugiere_comprar()
    {
        SembrarBusquedas(_api.ModeloDadoDeBaja, topes: [12_000, 15_000, 30_000]);

        var sugerencias = await SugerenciasAsync();
        var sugerencia = sugerencias.Single(s => s.ModeloId == _api.ModeloDadoDeBaja);

        Assert.Equal(nameof(TipoDeSugerencia.Comprar), sugerencia.Tipo);
        Assert.Equal(0, sugerencia.UnidadesEnStock);
        Assert.Equal(3, sugerencia.Visitas);

        // La mediana de los topes pedidos, no el promedio: los tres son 12, 15 y 30 mil, y
        // el promedio (19) diría que hay presupuesto donde no lo hay.
        Assert.Equal(15_000m, sugerencia.PresupuestoTipico);
        Assert.Equal("Usd", sugerencia.Moneda);
    }

    /// <summary>
    /// El mismo tipo de búsqueda, pero con unidades publicadas de ese modelo, manda al
    /// lado opuesto: el stock está y no encajó, así que lo que hay que revisar es el
    /// precio o el año, no la chequera.
    /// </summary>
    [Fact]
    public async Task Un_modelo_que_si_esta_en_stock_manda_a_revisar_lo_que_hay()
    {
        SembrarBusquedas(_api.ModeloId, topes: [8_000, 9_000, 10_000]);

        var sugerencias = await SugerenciasAsync();
        var sugerencia = sugerencias.Single(s => s.ModeloId == _api.ModeloId);

        Assert.Equal(nameof(TipoDeSugerencia.RevisarLoQueTenes), sugerencia.Tipo);
        Assert.True(sugerencia.UnidadesEnStock > 0);
    }

    /// <summary>
    /// Una sola persona buscando algo raro no es una señal de mercado. Si cada curioso
    /// generara una sugerencia, la pantalla sería ruido y la primera sugerencia buena se
    /// perdería en el medio.
    /// </summary>
    [Fact]
    public async Task Una_sola_visita_no_alcanza_para_sugerir_una_compra()
    {
        var filtros = JsonSerializer.Serialize(new { carroceria = "Convertible", moneda = "Usd", precioHasta = 40_000 });

        _api.ConLaBase(db =>
        {
            // Dos búsquedas, una sola visita: la misma persona insistiendo.
            db.Busquedas.Add(Busqueda(filtros, "unico-curioso"));
            db.Busquedas.Add(Busqueda(filtros, "unico-curioso"));
        });

        var sugerencias = await SugerenciasAsync();

        Assert.DoesNotContain(sugerencias, s => s.Carroceria == "Convertible");
    }

    /// <summary>
    /// Una búsqueda que solo pone un precio no se puede traducir a una compra: nadie sale
    /// a comprar "algo de hasta quince mil dólares". Se sigue viendo en el reporte de
    /// búsquedas, que es donde tiene sentido leerla.
    /// </summary>
    [Fact]
    public async Task Una_busqueda_solo_de_precio_no_genera_sugerencia()
    {
        var filtros = JsonSerializer.Serialize(new { moneda = "Usd", precioHasta = 15_000 });

        _api.ConLaBase(db =>
        {
            db.Busquedas.Add(Busqueda(filtros, "visita-a"));
            db.Busquedas.Add(Busqueda(filtros, "visita-b"));
            db.Busquedas.Add(Busqueda(filtros, "visita-c"));
            db.Busquedas.Add(Busqueda(filtros, "visita-d"));
        });

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var grupos = await cliente.GetFromJsonAsync<List<BusquedaSinResultadoDto>>(
            "/api/reportes/busquedas-sin-resultado?dias=90");

        var sugerencias = await SugerenciasAsync();

        Assert.NotNull(grupos);
        Assert.Contains(grupos, g => g.MarcaId is null && g.ModeloId is null && g.Carroceria is null);
        Assert.DoesNotContain(sugerencias, s => s.MarcaId is null && s.ModeloId is null && s.Carroceria is null);
    }

    /// <summary>Los reportes son del dueño, también este.</summary>
    [Fact]
    public async Task El_vendedor_no_ve_las_sugerencias()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/sugerencias");

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>Una búsqueda por modelo por cada tope, cada una de una visita distinta.</summary>
    private void SembrarBusquedas(int modeloId, IReadOnlyList<int> topes)
    {
        _api.ConLaBase(db =>
        {
            for (var i = 0; i < topes.Count; i++)
            {
                var filtros = JsonSerializer.Serialize(new
                {
                    modeloId,
                    anioDesde = 2016,
                    moneda = "Usd",
                    precioHasta = topes[i],
                });

                db.Busquedas.Add(Busqueda(filtros, $"visita-{modeloId}-{i}"));
            }
        });
    }

    private async Task<IReadOnlyList<SugerenciaDeCompraDto>> SugerenciasAsync()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var sugerencias = await cliente.GetFromJsonAsync<List<SugerenciaDeCompraDto>>("/api/reportes/sugerencias");

        Assert.NotNull(sugerencias);

        return sugerencias;
    }

    private Busqueda Busqueda(string filtros, string sesion) => new()
    {
        TenantId = _api.TenantNorte,
        Filtros = filtros,
        ResultadosCount = 0,
        SessionId = sesion,
        CreatedAt = DateTime.UtcNow.AddDays(-3),
    };
}
