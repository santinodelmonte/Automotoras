using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Reportes de demanda: que las señales digan lo que tienen que decir, y que sigan
/// respetando la frontera entre automotoras.
/// </summary>
public sealed class ReportesTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public ReportesTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_vendedor_no_entra_a_los_reportes()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/reportes/demanda");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// Muchas miradas y ninguna consulta es la señal clásica de precio alto. Es la lectura
    /// que justifica todo el tracking.
    /// </summary>
    [Fact]
    public async Task Muchas_vistas_sin_consultas_se_marcan_como_precio_alto()
    {
        SembrarEventos(_api.VehiculoDeNorte, vistas: 60, consultas: 0);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=90");

        Assert.NotNull(reporte);

        var analizado = reporte.Vehiculos.Single(v => v.VehiculoId == _api.VehiculoDeNorte);

        Assert.Equal(nameof(SenalDeDemanda.PrecioAlto), analizado.Senal);
        Assert.Equal(60, analizado.Vistas);
        Assert.Equal(0, analizado.Consultas);
        Assert.Contains("precio", analizado.Lectura, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Una unidad que lleva tiempo publicada y que casi nadie mira tiene otro problema, y
    /// el reporte no lo puede confundir con el de precio.
    /// </summary>
    [Fact]
    public async Task Mucho_tiempo_publicado_y_sin_visitas_se_marca_como_falta_de_interes()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var alta = await cliente.PostAsJsonAsync("/api/vehiculos", new GuardarVehiculoRequest(
            _api.ModeloId, null, 2012, 190_000, "Diesel", "Manual", "Blanco", 5, "2.0",
            6_500m, "Usd", null, false, null,
            DateTime.UtcNow.AddDays(-120)));

        alta.EnsureSuccessStatusCode();
        var olvidado = await alta.Content.ReadFromJsonAsync<VehiculoDto>();
        Assert.NotNull(olvidado);

        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=90");
        Assert.NotNull(reporte);

        var analizado = reporte.Vehiculos.Single(v => v.VehiculoId == olvidado.Id);

        Assert.Equal(nameof(SenalDeDemanda.SinInteres), analizado.Senal);
        Assert.True(analizado.DiasEnGondola >= 100);
    }

    /// <summary>
    /// Las búsquedas que no encontraron nada se agrupan por lo que se pidió: es lo más
    /// parecido a una lista de compras escrita por los propios compradores.
    /// </summary>
    [Fact]
    public async Task La_demanda_insatisfecha_se_agrupa_por_lo_que_se_pidio()
    {
        SembrarBusquedaSinResultado(
            new FiltrosRegistrados(null, null, 2020, null, "Usd", null, 12_000m, null, null, null, null, "Pickup"),
            veces: 4);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=90");

        Assert.NotNull(reporte);

        var pedido = reporte.DemandaInsatisfecha.Single(d => d.Carroceria == "Pickup");

        Assert.Equal(4, pedido.Veces);
        Assert.Equal(2020, pedido.AnioDesde);
        Assert.Contains("Pickup", pedido.Descripcion, StringComparison.Ordinal);
        Assert.Contains("2020", pedido.Descripcion, StringComparison.Ordinal);
    }

    /// <summary>
    /// El reporte es de la propia automotora. Los eventos de otra no lo tocan ni de lejos.
    /// </summary>
    [Fact]
    public async Task El_reporte_no_incluye_vehiculos_de_otra_automotora()
    {
        SembrarEventos(_api.VehiculoDeSur, vistas: 200, consultas: 0, tenantId: _api.TenantSur);

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=90");

        Assert.NotNull(reporte);
        Assert.DoesNotContain(reporte.Vehiculos, v => v.VehiculoId == _api.VehiculoDeSur);
    }

    /// <summary>Los vendidos no tienen decisión pendiente: no entran al reporte.</summary>
    [Fact]
    public async Task Los_vendidos_no_entran_al_reporte()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var reporte = await cliente.GetFromJsonAsync<ReporteDeDemandaDto>("/api/reportes/demanda?dias=90");

        Assert.NotNull(reporte);
        Assert.DoesNotContain(reporte.Vehiculos, v => v.VehiculoId == _api.VendidoDeNorte);
    }

    private void SembrarEventos(int vehiculoId, int vistas, int consultas, int? tenantId = null)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Sin tenant resuelto y escribiendo para uno concreto: se declara explícito, que es
        // exactamente lo que la política de escritura obliga a hacer.
        using var _ = db.PermitirEscrituraCrossTenant();

        var ahora = DateTime.UtcNow;

        for (var i = 0; i < vistas; i++)
        {
            db.Eventos.Add(new Evento
            {
                TenantId = tenantId ?? _api.TenantNorte,
                VehiculoId = vehiculoId,
                Tipo = TipoEvento.ViewFicha,
                CreatedAt = ahora.AddDays(-(i % 20)),
            });
        }

        for (var i = 0; i < consultas; i++)
        {
            db.Eventos.Add(new Evento
            {
                TenantId = tenantId ?? _api.TenantNorte,
                VehiculoId = vehiculoId,
                Tipo = TipoEvento.ClickWhatsapp,
                CreatedAt = ahora.AddDays(-(i % 20)),
            });
        }

        db.SaveChanges();
    }

    private void SembrarBusquedaSinResultado(FiltrosRegistrados filtros, int veces)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var _ = db.PermitirEscrituraCrossTenant();

        var json = JsonSerializer.Serialize(filtros, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        for (var i = 0; i < veces; i++)
        {
            db.Busquedas.Add(new Busqueda
            {
                TenantId = _api.TenantNorte,
                Filtros = json,
                ResultadosCount = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-i),
            });
        }

        db.SaveChanges();
    }
}
