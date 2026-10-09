using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El buscador de la portada: lo que se escribe y no está tiene que quedar registrado como
/// demanda, con la marca y el modelo resueltos contra el catálogo.
/// </summary>
public sealed class BusquedaDeTextoTests : IClassFixture<FabricaDeApi>
{
    private const string Url = "/t/norte/api/public/busquedas";

    private readonly FabricaDeApi _api;

    public BusquedaDeTextoTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// El caso por el que existe el endpoint: una marca que está en el catálogo pero no en
    /// el patio. El buscador no la sugiere, y sin esto la búsqueda se perdía.
    /// </summary>
    [Fact]
    public async Task Un_modelo_del_catalogo_que_no_esta_en_stock_queda_como_busqueda_vacia()
    {
        var hilux = AgregarAlCatalogo("Toyota", "Hilux", Carroceria.Pickup);
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            Url, new BusquedaDeTextoRequest("toyota hilux", Confirmada: false, "texto-hilux"));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        var (busqueda, evento) = await LeerAsync("texto-hilux");

        Assert.NotNull(busqueda);
        Assert.Equal(0, busqueda.ResultadosCount);
        Assert.Contains($"\"modeloId\":{hilux}", busqueda.Filtros, StringComparison.Ordinal);
        Assert.Contains("toyota hilux", busqueda.Filtros, StringComparison.Ordinal);
        Assert.NotNull(evento);
        Assert.Equal(TipoEvento.BusquedaSinResultado, evento.Tipo);
    }

    /// <summary>Mientras se escribe, un texto que no se parece a nada no se guarda: serían letras sueltas.</summary>
    [Fact]
    public async Task Un_texto_irreconocible_solo_se_guarda_si_se_confirma()
    {
        using var cliente = _api.CreateClient();

        await cliente.PostAsJsonAsync(Url, new BusquedaDeTextoRequest("zxq", Confirmada: false, "texto-raro"));
        Assert.Null((await LeerAsync("texto-raro")).Busqueda);

        await cliente.PostAsJsonAsync(Url, new BusquedaDeTextoRequest("zxq", Confirmada: true, "texto-raro"));
        var (busqueda, _) = await LeerAsync("texto-raro");

        Assert.NotNull(busqueda);
        Assert.Contains("zxq", busqueda.Filtros, StringComparison.Ordinal);
    }

    /// <summary>Lo que sí está publicado se cuenta con sus resultados y no deja evento de búsqueda vacía.</summary>
    [Fact]
    public async Task Lo_que_hay_en_stock_se_registra_con_sus_resultados()
    {
        using var cliente = _api.CreateClient();

        await cliente.PostAsJsonAsync(Url, new BusquedaDeTextoRequest("gol", Confirmada: true, "texto-gol"));

        var (busqueda, evento) = await LeerAsync("texto-gol");

        Assert.NotNull(busqueda);
        Assert.True(busqueda.ResultadosCount > 0);
        Assert.Null(evento);
    }

    [Fact]
    public async Task Sin_automotora_resuelta_responde_404()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/public/busquedas", new BusquedaDeTextoRequest("gol", Confirmada: true, null));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private int AgregarAlCatalogo(string nombreMarca, string nombreModelo, Carroceria carroceria)
    {
        var modelo = new Modelo { Marca = new Marca { Nombre = nombreMarca }, Nombre = nombreModelo, Carroceria = carroceria };
        _api.ConLaBase(db => db.Modelos.Add(modelo));
        return modelo.Id;
    }

    private async Task<(Busqueda? Busqueda, Evento? Evento)> LeerAsync(string sesion)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var busqueda = await db.Busquedas.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.SessionId == sesion);
        var evento = await db.Eventos.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.SessionId == sesion);

        return (busqueda, evento);
    }
}
