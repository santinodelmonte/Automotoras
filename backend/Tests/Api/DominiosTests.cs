using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// El alta de un dominio propio, de punta a punta y sin que nadie de la plataforma
/// intervenga.
/// </summary>
/// <remarks>
/// Lo que se prueba no es que el flujo ande, sino que no se pueda saltear: un dominio sin
/// verificar no sirve tráfico, y por lo tanto escribirlo en un formulario no alcanza para
/// quedarse con el dominio de otro.
/// </remarks>
public sealed class DominiosTests : IClassFixture<FabricaDeApi>
{
    private const string Dominio = "autosdelsur.com.uy";

    private readonly FabricaDeApi _api;

    public DominiosTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task El_vendedor_no_toca_los_dominios()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/dominios");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// El recorrido completo: alta, un intento fallido porque todavía no está el TXT, la
    /// publicación del TXT y la verificación.
    /// </summary>
    [Fact]
    public async Task Un_dominio_verifica_cuando_aparece_el_txt()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest(Dominio));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var creado = await alta.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(creado);
        Assert.Equal(nameof(EstadoDeDominio.Pendiente), creado.Estado);
        Assert.Equal($"_automotora.{Dominio}", creado.Verificacion.Nombre);

        // Sin el TXT no verifica, y la respuesta sigue siendo 200: que el DNS todavía no
        // propagó no es un error del pedido.
        var sinTxt = await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        sinTxt.EnsureSuccessStatusCode();

        var pendiente = await sinTxt.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(pendiente);
        Assert.Equal(nameof(EstadoDeDominio.Pendiente), pendiente.Estado);
        Assert.NotNull(pendiente.UltimoError);

        _api.Dns.Publicar(creado.Verificacion.Nombre, creado.Verificacion.Valor);

        var conTxt = await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        conTxt.EnsureSuccessStatusCode();

        var verificado = await conTxt.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(verificado);
        Assert.Equal(nameof(EstadoDeDominio.Verificado), verificado.Estado);
        Assert.NotNull(verificado.VerificadoEn);
        Assert.Null(verificado.UltimoError);
    }

    /// <summary>
    /// Lo que sostiene todo: un dominio pendiente no resuelve. Si resolviera, cualquiera se
    /// quedaría con el sitio de otro escribiendo su dominio en el formulario.
    /// </summary>
    [Fact]
    public async Task Un_dominio_sin_verificar_no_sirve_el_sitio_publico()
    {
        const string pendiente = "todavia-no-verificado.uy";

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest(pendiente));
        alta.EnsureSuccessStatusCode();

        using var visitante = _api.CreateClient();
        visitante.DefaultRequestHeaders.Host = pendiente;

        var respuesta = await visitante.GetAsync("/api/public/tenant");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_dominio_verificado_si_sirve_el_sitio_publico()
    {
        using var visitante = _api.CreateClient();
        visitante.DefaultRequestHeaders.Host = FabricaDeApi.DominioDeNorte;

        var respuesta = await visitante.GetAsync("/api/public/tenant");

        respuesta.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Un dominio ya dado de alta por otra automotora no se puede reclamar. Sin esto, dos
    /// filas iguales harían indeterminado el tenant que resuelve.
    /// </summary>
    [Fact]
    public async Task Un_dominio_ya_tomado_no_se_puede_reclamar()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerSur);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/dominios", new AgregarDominioRequest(FabricaDeApi.DominioDeNorte));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_dominio_mal_escrito_se_rechaza_en_la_validacion()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/dominios", new AgregarDominioRequest("https://con-esquema.uy/y-barra"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Un dominio verificado que deja de tener el TXT no se apaga al primer fallo: puede ser
    /// un DNS lento, y bajarle el sitio a una automotora por eso sería peor que esperar.
    /// </summary>
    [Fact]
    public async Task Un_dominio_verificado_aguanta_fallos_antes_de_caerse()
    {
        const string dominio = "aguanta-fallos.uy";

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest(dominio));
        var creado = await alta.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(creado);

        _api.Dns.Publicar(creado.Verificacion.Nombre, creado.Verificacion.Valor);
        await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);

        _api.Dns.Borrar(creado.Verificacion.Nombre);

        var primerFallo = await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        var tolerado = await primerFallo.Content.ReadFromJsonAsync<DominioDto>();

        Assert.NotNull(tolerado);
        Assert.Equal(nameof(EstadoDeDominio.Verificado), tolerado.Estado);
        Assert.NotNull(tolerado.UltimoError);

        // Con la tolerancia por defecto en tres, el tercer fallo seguido lo da por caído.
        await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        var tercerFallo = await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        var caido = await tercerFallo.Content.ReadFromJsonAsync<DominioDto>();

        Assert.NotNull(caido);
        Assert.Equal(nameof(EstadoDeDominio.Caido), caido.Estado);
        Assert.False(caido.EsPrincipal);
    }

    /// <summary>
    /// Un DNS que no contesta no prueba nada. Si contara como fallo, una caída del resolver
    /// apagaría los sitios de todas las automotoras a la vez.
    /// </summary>
    [Fact]
    public async Task Un_dns_que_no_contesta_no_cuenta_como_fallo()
    {
        const string dominio = "dns-caido.uy";

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest(dominio));
        var creado = await alta.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(creado);

        _api.Dns.Publicar(creado.Verificacion.Nombre, creado.Verificacion.Valor);
        await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);

        _api.Dns.HacerFallar(creado.Verificacion.Nombre);

        for (var intento = 0; intento < 5; intento++)
        {
            await cliente.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        }

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guardado = await db.Dominios
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Dominio == dominio);

        Assert.Equal(EstadoDeDominio.Verificado, guardado.Estado);
        Assert.Equal(0, guardado.VerificacionesFallidas);
    }

    /// <summary>
    /// Una automotora no puede tocar el dominio de otra ni sabiendo su id.
    /// </summary>
    [Fact]
    public async Task El_dominio_de_otra_automotora_no_existe()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest("solo-de-norte.uy"));
        var creado = await alta.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(creado);

        using var deSur = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerSur);

        var verificar = await deSur.PostAsync($"/api/dominios/{creado.Id}/verificar", content: null);
        var borrar = await deSur.DeleteAsync($"/api/dominios/{creado.Id}");

        Assert.Equal(HttpStatusCode.NotFound, verificar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrar.StatusCode);
    }
}

/// <summary>
/// La corrida del cron, en su propia clase y con su propio fixture.
/// </summary>
/// <remarks>
/// El job repasa los dominios de todas las automotoras, así que corriendo en la misma base
/// que los demás tests les movería el estado por debajo. Y como xUnit no garantiza el orden
/// dentro de una clase, el que fallara dependería de cuál corrió antes.
/// </remarks>
public sealed class DominiosDelCronTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public DominiosDelCronTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// El cron cierra el alta sin que nadie apriete nada, que es lo que hace que esto sea
    /// automático y no un formulario con un botón.
    /// </summary>
    [Fact]
    public async Task El_job_verifica_los_pendientes_sin_que_nadie_entre_al_panel()
    {
        const string dominio = "lo-cierra-el-cron.uy";

        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);
        var alta = await cliente.PostAsJsonAsync("/api/dominios", new AgregarDominioRequest(dominio));
        var creado = await alta.Content.ReadFromJsonAsync<DominioDto>();
        Assert.NotNull(creado);

        _api.Dns.Publicar(creado.Verificacion.Nombre, creado.Verificacion.Valor);

        using var cron = _api.CreateClient();
        cron.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var corrida = await cron.PostAsync("/api/jobs/verificar-dominios", content: null);
        corrida.EnsureSuccessStatusCode();

        var resumen = await corrida.Content.ReadFromJsonAsync<ResumenDeVerificaciones>();
        Assert.NotNull(resumen);
        Assert.True(resumen.Revisados > 0);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guardado = await db.Dominios
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Dominio == dominio);

        Assert.Equal(EstadoDeDominio.Verificado, guardado.Estado);
    }

    [Fact]
    public async Task El_job_de_dominios_pide_el_secreto()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync("/api/jobs/verificar-dominios", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
