using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// La verificación de los dominios propios.
/// </summary>
/// <remarks>
/// Lo que sostiene esto es una regla y no una comodidad: cargar un dominio es declarar una
/// intención, servirlo es otra cosa. Sin la verificación, cualquier automotora puede
/// escribir el dominio de otra empresa en su configuración y quedárselo para el día en que
/// ese dominio apunte para acá.
/// </remarks>
public sealed class VerificacionDeDominioTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public VerificacionDeDominioTests(FabricaDeApi api)
    {
        _api = api;
    }

    /// <summary>
    /// El caso que justifica todo: el dominio está cargado, pero nadie comprobó que apunte
    /// acá, así que el sitio no responde por él.
    /// </summary>
    [Fact]
    public async Task Un_dominio_sin_verificar_no_resuelve_el_sitio()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Host = FabricaDeApi.DominioSinVerificarDeSur;

        var respuesta = await cliente.GetAsync("/api/public/tenant");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>Y el mismo tenant sí responde por su slug: lo que falta es el dominio, no la automotora.</summary>
    [Fact]
    public async Task La_automotora_con_el_dominio_sin_verificar_sigue_andando_por_su_slug()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/t/sur/api/public/tenant");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// Con el DNS apuntando a una de las IP declaradas, el dominio queda verificado y el
    /// sitio empieza a responder por él en el mismo momento.
    /// </summary>
    [Fact]
    public async Task Un_dominio_que_apunta_a_la_aplicacion_queda_verificado_y_empieza_a_servir()
    {
        _api.Dns.Responder(FabricaDeApi.DominioSinVerificarDeSur, [FabricaDeApi.IpDeLaAplicacion]);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsync($"/api/admin/tenants/{_api.TenantSur}/verificar-dominio", null);
        var verificacion = await respuesta.Content.ReadFromJsonAsync<VerificacionDeDominioDto>();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(verificacion);
        Assert.Equal(nameof(ResultadoDeVerificacion.Verificado), verificacion.Resultado);
        Assert.NotNull(verificacion.VerificadoEn);

        using var visitante = _api.CreateClient();
        visitante.DefaultRequestHeaders.Host = FabricaDeApi.DominioSinVerificarDeSur;

        var sitio = await visitante.GetAsync("/api/public/tenant");

        Assert.Equal(HttpStatusCode.OK, sitio.StatusCode);
    }

    /// <summary>
    /// Un dominio que resuelve a otro servidor no se verifica. Es el intento de reservarse
    /// el dominio de otro: existe, resuelve, pero no es de quien lo cargó.
    /// </summary>
    [Fact]
    public async Task Un_dominio_que_apunta_a_otro_lado_no_se_verifica()
    {
        _api.ConLaBase(db =>
        {
            var tenant = db.Tenants.IgnoreQueryFilters().Single(t => t.Slug == "norte");
            tenant.DominioCustom = "dominio-ajeno.uy";
            tenant.DominioVerificadoEn = null;
        });

        _api.Dns.Responder("dominio-ajeno.uy", ["203.0.113.77"]);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsync($"/api/admin/tenants/{_api.TenantNorte}/verificar-dominio", null);
        var verificacion = await respuesta.Content.ReadFromJsonAsync<VerificacionDeDominioDto>();

        Assert.NotNull(verificacion);
        Assert.Equal(nameof(ResultadoDeVerificacion.ApuntaAOtroLado), verificacion.Resultado);
        Assert.Null(verificacion.VerificadoEn);

        // Y la respuesta dice a dónde apunta, que es lo único con lo que quien configura el
        // DNS puede darse cuenta de qué cargó mal.
        Assert.Contains("203.0.113.77", verificacion.ApuntaA);
        Assert.Contains(FabricaDeApi.IpDeLaAplicacion, verificacion.DeberiaApuntarA);
    }

    /// <summary>Un dominio que todavía no resuelve a nada se distingue del que resuelve mal.</summary>
    [Fact]
    public async Task Un_dominio_que_no_resuelve_lo_dice_asi()
    {
        _api.ConLaBase(db =>
        {
            var tenant = db.Tenants.IgnoreQueryFilters().Single(t => t.Slug == "norte");
            tenant.DominioCustom = "todavia-no-existe.uy";
            tenant.DominioVerificadoEn = null;
        });

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsync($"/api/admin/tenants/{_api.TenantNorte}/verificar-dominio", null);
        var verificacion = await respuesta.Content.ReadFromJsonAsync<VerificacionDeDominioDto>();

        Assert.NotNull(verificacion);
        Assert.Equal(nameof(ResultadoDeVerificacion.NoResuelve), verificacion.Resultado);
    }

    /// <summary>
    /// Cambiar el dominio invalida el sello anterior: era sobre otro dominio. Sin esto, se
    /// verifica uno propio y después se lo reemplaza por el de otra empresa.
    /// </summary>
    [Fact]
    public async Task Cambiar_el_dominio_invalida_la_verificacion()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var actualizado = await admin.PutAsJsonAsync(
            $"/api/admin/tenants/{_api.TenantNorte}",
            new ActualizarTenantRequest("norte", "Automotora Norte", "otro-dominio.uy", Activo: true));

        actualizado.EnsureSuccessStatusCode();

        var tenant = await actualizado.Content.ReadFromJsonAsync<TenantAdminDto>();

        Assert.NotNull(tenant);
        Assert.Null(tenant.DominioVerificadoEn);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guardado = await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == _api.TenantNorte);

        Assert.Null(guardado.DominioVerificadoEn);
    }

    /// <summary>Verificar dominios es del SuperAdmin: decide qué dominio sirve a qué automotora.</summary>
    [Fact]
    public async Task El_owner_no_puede_verificar_dominios()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PostAsync($"/api/admin/tenants/{_api.TenantNorte}/verificar-dominio", null);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}

/// <summary>
/// La evaluación, sin base y sin HTTP.
/// </summary>
public sealed class EvaluacionDeDominioTests
{
    private static readonly string[] Nuestras = ["190.0.0.1", "190.0.0.2"];

    [Fact]
    public void Alcanza_con_que_una_direccion_coincida()
        => Assert.Equal(
            ResultadoDeVerificacion.Verificado,
            VerificacionDeDominio.Evaluar("x.uy", ["203.0.113.9", "190.0.0.2"], Nuestras));

    [Fact]
    public void Sin_dominio_no_hay_nada_que_verificar()
        => Assert.Equal(
            ResultadoDeVerificacion.SinDominio,
            VerificacionDeDominio.Evaluar(null, ["190.0.0.1"], Nuestras));

    /// <summary>
    /// Sin IP declaradas no se da nada por bueno. Una configuración incompleta que verifique
    /// cualquier dominio es peor que una que no verifique ninguno.
    /// </summary>
    [Fact]
    public void Sin_ips_declaradas_falla_cerrado()
        => Assert.Equal(
            ResultadoDeVerificacion.SinIpsDeclaradas,
            VerificacionDeDominio.Evaluar("x.uy", ["190.0.0.1"], []));

    [Fact]
    public void Un_dominio_que_no_resuelve_se_distingue_del_que_resuelve_mal()
    {
        Assert.Equal(
            ResultadoDeVerificacion.NoResuelve,
            VerificacionDeDominio.Evaluar("x.uy", [], Nuestras));

        Assert.Equal(
            ResultadoDeVerificacion.ApuntaAOtroLado,
            VerificacionDeDominio.Evaluar("x.uy", ["203.0.113.9"], Nuestras));
    }
}
