using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Catálogo público: qué se ve, qué no, y qué queda registrado de cada visita.
/// </summary>
public sealed class SitioPublicoVehiculosTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public SitioPublicoVehiculosTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_listado_publico_solo_muestra_los_de_esa_automotora()
    {
        using var cliente = _api.CreateClient();

        var pagina = await cliente.GetFromJsonAsync<PaginaDe<VehiculoPublicoResumenDto>>(
            "/t/norte/api/public/vehiculos");

        Assert.NotNull(pagina);
        Assert.NotEmpty(pagina.Items);
        Assert.DoesNotContain(pagina.Items, v => v.Id == _api.VehiculoDeSur);
    }

    /// <summary>Los vendidos salen del listado y se mantienen en la base.</summary>
    [Fact]
    public async Task Los_vendidos_no_aparecen_en_el_sitio_publico()
    {
        using var cliente = _api.CreateClient();

        var pagina = await cliente.GetFromJsonAsync<PaginaDe<VehiculoPublicoResumenDto>>(
            "/t/norte/api/public/vehiculos");

        Assert.NotNull(pagina);
        Assert.DoesNotContain(pagina.Items, v => v.Id == _api.VendidoDeNorte);

        var ficha = await cliente.GetAsync($"/t/norte/api/public/vehiculos/{_api.VendidoDeNorte}");
        Assert.Equal(HttpStatusCode.NotFound, ficha.StatusCode);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sigueEnLaBase = await db.Vehiculos
            .IgnoreQueryFilters()
            .AnyAsync(v => v.Id == _api.VendidoDeNorte);

        Assert.True(sigueEnLaBase);
    }

    /// <summary>
    /// El criterio de aceptación cinco: marcar vendido saca la unidad del sitio en el acto
    /// y deja registrada la fecha y el precio de venta.
    /// </summary>
    [Fact]
    public async Task Marcar_vendido_saca_el_vehiculo_del_sitio_publico_de_inmediato()
    {
        using var panel = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        using var publico = _api.CreateClient();

        var creado = await panel.PostAsJsonAsync("/api/vehiculos", new GuardarVehiculoRequest(
            _api.ModeloId, null, 2018, 70_000, "Diesel", "Automatica",
            "Negro", 5, "2.0", 21_000m, "Usd", null, false, null, null));

        creado.EnsureSuccessStatusCode();
        var vehiculo = await creado.Content.ReadFromJsonAsync<VehiculoDto>();
        Assert.NotNull(vehiculo);

        var antes = await publico.GetAsync($"/t/norte/api/public/vehiculos/{vehiculo.Id}");
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);

        var venta = await panel.PostAsJsonAsync(
            $"/api/vehiculos/{vehiculo.Id}/estado",
            new CambiarEstadoRequest("Vendido", new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), 20_000m));

        venta.EnsureSuccessStatusCode();
        var vendido = await venta.Content.ReadFromJsonAsync<VehiculoDto>();
        Assert.NotNull(vendido);
        Assert.Equal("Vendido", vendido.Estado);
        Assert.Equal(20_000m, vendido.PrecioVenta);
        Assert.NotNull(vendido.FechaVenta);

        var despues = await publico.GetAsync($"/t/norte/api/public/vehiculos/{vehiculo.Id}");
        Assert.Equal(HttpStatusCode.NotFound, despues.StatusCode);
    }

    [Fact]
    public async Task La_ficha_publica_trae_el_mensaje_de_whatsapp_ya_armado()
    {
        using var cliente = _api.CreateClient();

        var ficha = await cliente.GetFromJsonAsync<VehiculoPublicoDto>(
            $"/t/norte/api/public/vehiculos/{_api.VehiculoDeNorte}");

        Assert.NotNull(ficha);
        Assert.Contains("Volkswagen Gol 2019", ficha.MensajeDeWhatsapp, StringComparison.Ordinal);
        Assert.Contains("Automotora Norte", ficha.Titulo, StringComparison.Ordinal);
    }

    /// <summary>
    /// Un vehículo de otra automotora no se ve desde este sitio, aunque el id sea correcto.
    /// </summary>
    [Fact]
    public async Task La_ficha_de_un_vehiculo_de_otra_automotora_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync($"/t/norte/api/public/vehiculos/{_api.VehiculoDeSur}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// Un rango de precio sin moneda mezclaría dólares con pesos y devolvería cualquier
    /// cosa. Mejor pedir la moneda que contestar un listado sin sentido.
    /// </summary>
    [Fact]
    public async Task Filtrar_por_precio_sin_moneda_se_rechaza()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/api/public/vehiculos?precioDesde=1000");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Los_filtros_recortan_el_listado()
    {
        using var cliente = _api.CreateClient();

        var pagina = await cliente.GetFromJsonAsync<PaginaDe<VehiculoPublicoResumenDto>>(
            "/t/norte/api/public/vehiculos?anioDesde=2030");

        Assert.NotNull(pagina);
        Assert.Empty(pagina.Items);
        Assert.Equal(0, pagina.Total);
    }

    /// <summary>
    /// Una búsqueda sin resultados es la señal más valiosa del producto: dice qué le están
    /// pidiendo a la automotora que no tiene. Queda registrada y además deja su evento.
    /// </summary>
    [Fact]
    public async Task Una_busqueda_sin_resultados_queda_registrada_con_su_evento()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/api/public/vehiculos?anioDesde=2031&sessionId=test-sin");
        respuesta.EnsureSuccessStatusCode();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var busqueda = await db.Busquedas
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.SessionId == "test-sin");

        Assert.NotNull(busqueda);
        Assert.Equal(0, busqueda.ResultadosCount);
        Assert.Equal(_api.TenantNorte, busqueda.TenantId);
        Assert.Contains("2031", busqueda.Filtros, StringComparison.Ordinal);

        var evento = await db.Eventos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.SessionId == "test-sin");

        Assert.NotNull(evento);
        Assert.Equal(TipoEvento.BusquedaSinResultado, evento.Tipo);
    }

    /// <summary>
    /// Entrar al listado sin filtrar no es una búsqueda. Registrarlo llenaría la tabla de
    /// ruido que después hay que descartar en cada reporte.
    /// </summary>
    [Fact]
    public async Task Un_listado_sin_filtros_no_registra_busqueda()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/api/public/vehiculos?sessionId=test-sin-filtros");
        respuesta.EnsureSuccessStatusCode();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hay = await db.Busquedas
            .IgnoreQueryFilters()
            .AnyAsync(b => b.SessionId == "test-sin-filtros");

        Assert.False(hay);
    }

    [Fact]
    public async Task La_home_trae_destacados_recientes_y_el_total()
    {
        using var cliente = _api.CreateClient();

        var home = await cliente.GetFromJsonAsync<HomePublicaDto>("/t/norte/api/public/home");

        Assert.NotNull(home);
        Assert.True(home.TotalDisponibles > 0);
        Assert.DoesNotContain(home.Recientes, v => v.Id == _api.VendidoDeNorte);
    }

    [Fact]
    public async Task El_sitemap_lista_solo_lo_publicado()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/norte/api/public/sitemap.xml");
        respuesta.EnsureSuccessStatusCode();

        var xml = await respuesta.Content.ReadAsStringAsync();

        Assert.Contains($"/vehiculos/{_api.VehiculoDeNorte}", xml, StringComparison.Ordinal);
        Assert.DoesNotContain($"/vehiculos/{_api.VendidoDeNorte}", xml, StringComparison.Ordinal);
    }
}
