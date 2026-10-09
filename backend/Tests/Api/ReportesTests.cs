using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Reportes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Los reportes de demanda de la fase 2.
/// </summary>
/// <remarks>
/// Se testean contra la API entera y no contra las consultas sueltas porque lo que hay
/// que garantizar es de a dos cosas a la vez: que la cuenta esté bien y que sea la del
/// tenant que preguntó. Un reporte correcto con los eventos de otra automotora adentro
/// sería peor que uno roto — nadie lo notaría.
/// </remarks>
public sealed class ReportesTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public ReportesTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// El reporte es del dueño. El vendedor carga stock y atiende consultas; lo que se
    /// compra y a qué precio no es su decisión.
    /// </summary>
    [Fact]
    public async Task El_vendedor_no_entra_a_los_reportes()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/demanda");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// Muchas vistas y casi ninguna consulta es la señal de que el precio está alto, y es
    /// toda la razón de ser del reporte.
    /// </summary>
    /// <remarks>
    /// La consulta suelta no está de adorno: la unidad sembrada lleva más de dos meses
    /// publicada, y sin una sola consulta la señal correcta sería <c>Estancado</c>. Que
    /// alguien haya preguntado es justamente lo que descarta el abandono y deja al precio
    /// como el sospechoso.
    /// </remarks>
    [Fact]
    public async Task Muchas_vistas_y_pocas_consultas_marcan_el_precio_alto()
    {
        var ahora = DateTime.UtcNow;

        _api.ConLaBase(db =>
        {
            for (var i = 0; i < 40; i++)
            {
                db.Eventos.Add(FabricaDeApi.Evento(
                    _api.TenantNorte, _api.VehiculoDeNorte, TipoEvento.ViewFicha, ahora.AddDays(-1), $"v-{i}"));
            }

            db.Eventos.Add(FabricaDeApi.Evento(
                _api.TenantNorte, _api.VehiculoDeNorte, TipoEvento.ClickWhatsapp, ahora.AddDays(-1), "v-0"));
        });

        var reporte = await DemandaAsync(dias: 30);
        var fila = reporte.Vehiculos.Single(v => v.VehiculoId == _api.VehiculoDeNorte);

        Assert.Equal(40, fila.Vistas);
        Assert.Equal(1, fila.Consultas);
        Assert.Equal(2.5m, fila.ConsultasPorCienVistas);
        Assert.Equal(nameof(SenalDeDemanda.PrecioAlto), fila.Senal);
    }

    /// <summary>
    /// Y sin ninguna consulta, después de dos meses publicada, la lectura es otra: no hay
    /// nada que ajustar en el precio si nadie llegó a preguntar.
    /// </summary>
    [Fact]
    public async Task Sin_consultas_y_con_meses_en_gondola_la_unidad_esta_estancada()
    {
        var reporte = await DemandaAsync(dias: 30);
        var fila = reporte.Vehiculos.Single(v => v.VehiculoId == _api.OlvidadoDeNorte);

        Assert.True(
            fila.DiasEnGondola >= UmbralesDeDemanda.DiasParaEstarEstancado,
            "La unidad sembrada tiene que llevar más de dos meses publicada.");

        Assert.Equal(0, fila.Consultas);
        Assert.Equal(nameof(SenalDeDemanda.Estancado), fila.Senal);
    }

    /// <summary>
    /// Los eventos de la otra automotora no entran en el reporte de esta. Es el mismo
    /// aislamiento de siempre, pero acá el que lo rompe no rompe una pantalla: contamina
    /// una decisión de compra.
    /// </summary>
    [Fact]
    public async Task Las_vistas_de_la_otra_automotora_no_entran_en_el_reporte()
    {
        var ahora = DateTime.UtcNow;

        _api.ConLaBase(db =>
        {
            for (var i = 0; i < 5; i++)
            {
                db.Eventos.Add(FabricaDeApi.Evento(
                    _api.TenantSur, _api.VehiculoDeSur, TipoEvento.ViewFicha, ahora.AddDays(-2), $"sur-{i}"));
            }
        });

        var reporte = await DemandaAsync(dias: 30);

        Assert.DoesNotContain(reporte.Vehiculos, v => v.VehiculoId == _api.VehiculoDeSur);
    }

    /// <summary>
    /// Fuera de la ventana no se cuenta. Sin esto, el reporte de treinta días diría lo
    /// mismo todos los meses y no habría forma de ver si algo mejoró.
    /// </summary>
    [Fact]
    public async Task Los_eventos_viejos_quedan_fuera_de_la_ventana()
    {
        var ahora = DateTime.UtcNow;

        _api.ConLaBase(db => db.Eventos.Add(FabricaDeApi.Evento(
            _api.TenantNorte, _api.VehiculoDeNorte, TipoEvento.ViewFicha, ahora.AddDays(-100), "vieja")));

        var deTreinta = await DemandaAsync(dias: 30);
        var deUnAnio = await DemandaAsync(dias: 365);

        var enTreinta = deTreinta.Vehiculos.Single(v => v.VehiculoId == _api.VehiculoDeNorte).Vistas;
        var enUnAnio = deUnAnio.Vehiculos.Single(v => v.VehiculoId == _api.VehiculoDeNorte).Vistas;

        Assert.True(enUnAnio > enTreinta, "La vista de hace cien días tiene que aparecer en el año y no en el mes.");
    }

    /// <summary>
    /// El reporte cubre lo que está publicado. Un vendido con cero vistas no dice que
    /// nadie lo quiera: dice que ya no se puede ver.
    /// </summary>
    [Fact]
    public async Task El_reporte_no_incluye_los_vendidos()
    {
        var reporte = await DemandaAsync(dias: 30);

        Assert.DoesNotContain(reporte.Vehiculos, v => v.VehiculoId == _api.VendidoDeNorte);
        Assert.Contains(reporte.Vehiculos, v => v.VehiculoId == _api.VehiculoDeNorte);
    }

    /// <summary>
    /// Las búsquedas vacías se agrupan por lo que se estaba buscando, y lo que se cuenta
    /// son las visitas distintas: veinte búsquedas de una sola persona indecisa no son
    /// demanda.
    /// </summary>
    [Fact]
    public async Task Las_busquedas_sin_resultado_se_agrupan_y_cuentan_visitas_distintas()
    {
        var ahora = DateTime.UtcNow;

        var pickup = JsonSerializer.Serialize(
            new { carroceria = "Pickup", anioDesde = 2018, moneda = "Usd", precioHasta = 25000 });

        var mismaPickupOtroPrecio = JsonSerializer.Serialize(
            new { carroceria = "Pickup", anioDesde = 2019, moneda = "Usd", precioHasta = 30000 });

        _api.ConLaBase(db =>
        {
            // Tres búsquedas de pickup, dos visitas distintas: la misma persona buscó dos
            // veces moviendo el precio, y eso es un solo interesado.
            db.Busquedas.Add(Busqueda(_api.TenantNorte, pickup, ahora.AddDays(-1), "visita-1"));
            db.Busquedas.Add(Busqueda(_api.TenantNorte, mismaPickupOtroPrecio, ahora.AddDays(-1), "visita-1"));
            db.Busquedas.Add(Busqueda(_api.TenantNorte, pickup, ahora.AddDays(-2), "visita-2"));

            // Y una del otro tenant, que no tiene que aparecer.
            db.Busquedas.Add(Busqueda(_api.TenantSur, pickup, ahora.AddDays(-1), "visita-sur"));
        });

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var grupos = await cliente.GetFromJsonAsync<List<BusquedaSinResultadoDto>>(
            "/api/reportes/busquedas-sin-resultado?dias=30");

        Assert.NotNull(grupos);

        var pickups = grupos.Single(g => g.Carroceria == "Pickup");

        Assert.Equal(3, pickups.Veces);
        Assert.Equal(2, pickups.Sesiones);

        // El rango pedido es la unión de lo que pidieron: desde el año más viejo hasta el
        // presupuesto más alto que alguien puso.
        Assert.Equal(2018, pickups.AnioDesde);
        Assert.Equal(30_000m, pickups.PrecioHasta);
        Assert.Equal("Usd", pickups.Moneda);
    }

    /// <summary>
    /// Una búsqueda que sí encontró algo no es demanda insatisfecha. Si se colara, el
    /// reporte diría que falta stock que está.
    /// </summary>
    [Fact]
    public async Task Las_busquedas_con_resultados_no_salen_en_el_reporte()
    {
        var filtros = JsonSerializer.Serialize(new { carroceria = "Convertible" });

        _api.ConLaBase(db => db.Busquedas.Add(new Busqueda
        {
            TenantId = _api.TenantNorte,
            Filtros = filtros,
            ResultadosCount = 4,
            SessionId = "con-resultados",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        }));

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var grupos = await cliente.GetFromJsonAsync<List<BusquedaSinResultadoDto>>(
            "/api/reportes/busquedas-sin-resultado?dias=30");

        Assert.NotNull(grupos);
        Assert.DoesNotContain(grupos, g => g.Carroceria == "Convertible");
    }

    /// <summary>
    /// Una fila con el JSON roto se descarta sola. Es analítica: que una búsqueda mal
    /// guardada tire el reporte entero sería cambiar un dato perdido por una pantalla que
    /// no carga.
    /// </summary>
    [Fact]
    public async Task Una_busqueda_con_el_json_roto_no_voltea_el_reporte()
    {
        _api.ConLaBase(db => db.Busquedas.Add(new Busqueda
        {
            TenantId = _api.TenantNorte,
            Filtros = "{esto no es json",
            ResultadosCount = 0,
            SessionId = "rota",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        }));

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/busquedas-sin-resultado?dias=30");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>La ventana se acota: nadie pide diez años de eventos con un GROUP BY encima.</summary>
    [Fact]
    public async Task La_ventana_se_recorta_a_los_topes()
    {
        var reporte = await DemandaAsync(dias: 5_000);

        Assert.Equal(VentanaDeReporte.DiasMaximo, reporte.Resumen.Dias);
    }

    private async Task<ReporteDeDemandaDto> DemandaAsync(int dias)
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>($"/api/reportes/demanda?dias={dias}");

        Assert.NotNull(reporte);

        return reporte;
    }

    private static Busqueda Busqueda(int tenantId, string filtros, DateTime cuando, string sesion) => new()
    {
        TenantId = tenantId,
        Filtros = filtros,
        ResultadosCount = 0,
        SessionId = sesion,
        CreatedAt = cuando,
    };
}

/// <summary>
/// La clasificación de una unidad, sin base y sin HTTP: es una regla de negocio y merece
/// leerse como tal.
/// </summary>
public sealed class UmbralesDeDemandaTests
{
    [Theory]
    // Sin una sola vista no hay nada que decir, y decir "el precio está alto" sería inventar.
    [InlineData(0, 0, 3, SenalDeDemanda.SinDatos)]
    // Tráfico insuficiente: el ratio de tres vistas es ruido, no una medición.
    [InlineData(3, 0, 3, SenalDeDemanda.SinVisibilidad)]
    [InlineData(15, 0, 3, SenalDeDemanda.SinDatos)]
    // Cuarenta vistas y una consulta: 2,5 cada cien, debajo del umbral.
    [InlineData(40, 1, 3, SenalDeDemanda.PrecioAlto)]
    [InlineData(40, 5, 3, SenalDeDemanda.Saludable)]
    // Dos meses en góndola sin que nadie pregunte, por más que se mire.
    [InlineData(200, 0, 90, SenalDeDemanda.Estancado)]
    // Con consultas, el tiempo en góndola solo no alcanza para llamarlo estancado.
    [InlineData(200, 20, 90, SenalDeDemanda.Saludable)]
    public void Clasifica_segun_los_umbrales(int vistas, int consultas, int dias, SenalDeDemanda esperada)
        => Assert.Equal(esperada, UmbralesDeDemanda.Clasificar(vistas, consultas, dias));

    [Fact]
    public void El_ratio_sin_vistas_es_cero_y_no_una_division_por_cero()
        => Assert.Equal(0m, UmbralesDeDemanda.ConsultasPorCienVistas(0, 0));
}
