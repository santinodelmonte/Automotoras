using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Vehiculos;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// ABM de vehículos: aislamiento, normalización del catálogo y el recorte del precio de
/// costo por rol.
/// </summary>
public sealed class VehiculosTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public VehiculosTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_listado_no_trae_vehiculos_de_otra_automotora()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var pagina = await cliente.GetFromJsonAsync<PaginaDe<VehiculoResumenDto>>("/api/vehiculos");

        Assert.NotNull(pagina);
        Assert.DoesNotContain(pagina.Items, v => v.Id == _api.VehiculoDeSur);
        Assert.Contains(pagina.Items, v => v.Id == _api.VehiculoDeNorte);
    }

    /// <summary>El criterio de aceptación uno, ahora sobre el recurso central del producto.</summary>
    [Fact]
    public async Task Pedir_por_id_un_vehiculo_de_otra_automotora_responde_404()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.GetAsync($"/api/vehiculos/{_api.VehiculoDeSur}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editar_un_vehiculo_de_otra_automotora_responde_404()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/vehiculos/{_api.VehiculoDeSur}",
            Nuevo(_api.ModeloId));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// El precio de costo es del dueño. No se esconde en la pantalla: no sale del servidor.
    /// </summary>
    [Fact]
    public async Task El_vendedor_no_recibe_el_precio_de_costo()
    {
        using var deVendedor = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);
        using var deOwner = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var paraVendedor = await deVendedor.GetFromJsonAsync<VehiculoDto>($"/api/vehiculos/{_api.VehiculoDeNorte}");
        var paraOwner = await deOwner.GetFromJsonAsync<VehiculoDto>($"/api/vehiculos/{_api.VehiculoDeNorte}");

        Assert.NotNull(paraVendedor);
        Assert.NotNull(paraOwner);
        Assert.Null(paraVendedor.PrecioCosto);
        Assert.NotNull(paraOwner.PrecioCosto);
    }

    [Fact]
    public async Task Un_vendedor_no_puede_escribir_el_precio_de_costo()
    {
        using var deVendedor = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);
        using var deOwner = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await deVendedor.PostAsJsonAsync(
            "/api/vehiculos",
            Nuevo(_api.ModeloId) with { PrecioCosto = 1m });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<VehiculoDto>();
        Assert.NotNull(creado);

        var visto = await deOwner.GetFromJsonAsync<VehiculoDto>($"/api/vehiculos/{creado.Id}");
        Assert.NotNull(visto);
        Assert.Null(visto.PrecioCosto);
    }

    /// <summary>
    /// La normalización es el cimiento de la analítica: si el select encadenado se pudiera
    /// saltear mandando ids sueltos, el catálogo dejaría de significar algo.
    /// </summary>
    [Fact]
    public async Task No_se_puede_cargar_un_vehiculo_con_un_modelo_dado_de_baja()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync("/api/vehiculos", Nuevo(_api.ModeloDadoDeBaja));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_cargar_un_vehiculo_con_un_modelo_inexistente()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync("/api/vehiculos", Nuevo(999_999));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_anio_imposible_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/vehiculos",
            Nuevo(_api.ModeloId) with { Anio = 20255 });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Sin fecha ni precio de venta no hay días en góndola ni margen, que es la mitad de
    /// para qué existe el producto.
    /// </summary>
    [Fact]
    public async Task Marcar_vendido_sin_fecha_ni_precio_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/vehiculos/{_api.VehiculoDeNorte}/estado",
            new CambiarEstadoRequest("Vendido", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Solo_el_owner_borra_vehiculos()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.DeleteAsync($"/api/vehiculos/{_api.VehiculoDeNorte}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static GuardarVehiculoRequest Nuevo(int modeloId) => new(
        ModeloId: modeloId,
        VersionId: null,
        Anio: 2020,
        Kilometraje: 40_000,
        Combustible: "Nafta",
        Transmision: "Manual",
        Color: "Gris",
        Puertas: 5,
        Motor: "1.6",
        Precio: 17_500m,
        Moneda: "Usd",
        Descripcion: "Impecable.",
        Destacado: false,
        PrecioCosto: null,
        FechaPublicacion: null);
}
