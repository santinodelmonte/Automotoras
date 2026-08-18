using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Dashboard;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Publico;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Tracking, tablero, panel de SuperAdmin y jobs.
/// </summary>
public sealed class AnaliticaYAdminTests : IClassFixture<FabricaDeApi>
{
    private readonly FabricaDeApi _api;

    public AnaliticaYAdminTests(FabricaDeApi api)
    {
        _api = api;
    }

    [Fact]
    public async Task Un_evento_de_ficha_queda_registrado_con_el_tenant_del_sitio()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/t/norte/api/public/events",
            new RegistrarEventoRequest("ViewFicha", _api.VehiculoDeNorte, "sesion-1"));

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evento = await db.Eventos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.SessionId == "sesion-1");

        Assert.NotNull(evento);
        Assert.Equal(_api.TenantNorte, evento.TenantId);
        Assert.Equal(TipoEvento.ViewFicha, evento.Tipo);
        Assert.Equal(_api.VehiculoDeNorte, evento.VehiculoId);
    }

    /// <summary>La IP nunca se guarda en claro.</summary>
    [Fact]
    public async Task La_ip_del_evento_se_guarda_hasheada()
    {
        using var cliente = _api.CreateClient();

        await cliente.PostAsJsonAsync(
            "/t/norte/api/public/events",
            new RegistrarEventoRequest("ClickWhatsapp", _api.VehiculoDeNorte, "sesion-ip"));

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evento = await db.Eventos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.SessionId == "sesion-ip");

        Assert.NotNull(evento);

        if (evento.IpHash is not null)
        {
            // SHA-256 en hexadecimal, y nada que se parezca a una dirección.
            Assert.Equal(64, evento.IpHash.Length);
            Assert.DoesNotContain('.', evento.IpHash);
            Assert.DoesNotContain(':', evento.IpHash);
        }
    }

    /// <summary>
    /// Sin esto, cualquiera podría inflarle las visitas a la unidad de otra automotora
    /// desde el sitio de la propia.
    /// </summary>
    [Fact]
    public async Task No_se_puede_registrar_un_evento_sobre_un_vehiculo_de_otra_automotora()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/t/norte/api/public/events",
            new RegistrarEventoRequest("ViewFicha", _api.VehiculoDeSur, "sesion-ajena"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_evento_de_ficha_sin_vehiculo_se_rechaza()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/t/norte/api/public/events",
            new RegistrarEventoRequest("ViewFicha", null, "sesion-x"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_dashboard_cuenta_el_stock_de_la_propia_automotora()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var tablero = await cliente.GetFromJsonAsync<DashboardDto>("/api/dashboard");

        Assert.NotNull(tablero);
        Assert.True(tablero.TotalDeVehiculos > 0);
        Assert.Contains(tablero.VehiculosPorEstado, c => c.Estado == nameof(EstadoVehiculo.Disponible));
    }

    [Fact]
    public async Task El_vendedor_no_entra_al_dashboard()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailVendedorNorte);

        var respuesta = await cliente.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_owner_configura_su_automotora_y_no_toca_el_slug()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/tenant",
            new GuardarConfiguracionRequest("Automotora Norte", "#123456", null, "+59899111222", null, null));

        respuesta.EnsureSuccessStatusCode();

        var configuracion = await respuesta.Content.ReadFromJsonAsync<ConfiguracionDeTenantDto>();

        Assert.NotNull(configuracion);
        Assert.Equal("#123456", configuracion.ColorPrimario);
        Assert.Equal("norte", configuracion.Slug);
    }

    [Fact]
    public async Task Un_color_que_no_es_hexadecimal_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/tenant",
            new GuardarConfiguracionRequest("Automotora Norte", "verde", null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_owner_no_entra_al_panel_de_superadmin()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.GetAsync("/api/admin/tenants");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// El SuperAdmin sí ve todas las automotoras: es el único lugar cross-tenant, y por eso
    /// vive bajo /api/admin/*.
    /// </summary>
    [Fact]
    public async Task El_superadmin_ve_todas_las_automotoras_con_sus_conteos()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var tenants = await cliente.GetFromJsonAsync<List<TenantAdminDto>>("/api/admin/tenants");

        Assert.NotNull(tenants);
        Assert.Contains(tenants, t => t.Slug == "norte");
        Assert.Contains(tenants, t => t.Slug == "sur");
        Assert.Contains(tenants, t => t.Slug == "apagada");
        Assert.True(tenants.First(t => t.Slug == "norte").Usuarios > 0);
    }

    /// <summary>
    /// El alta crea la automotora y su Owner en la misma operación: una automotora sin
    /// nadie que pueda entrar no sirve para nada.
    /// </summary>
    [Fact]
    public async Task El_superadmin_crea_una_automotora_con_su_owner_y_ese_owner_puede_entrar()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "este", "Automotora Este", "owner@este.uy", "Owner Este", "Clave-nueva-9"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var sesion = await _api.LoginAsync("owner@este.uy", "Clave-nueva-9");

        Assert.Equal("Owner", sesion.Usuario.Rol);
        Assert.NotNull(sesion.Usuario.TenantId);
        Assert.NotEqual(_api.TenantNorte, sesion.Usuario.TenantId);
    }

    [Fact]
    public async Task Un_slug_repetido_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "norte", "Otra Norte", "otro@norte.uy", "Otro", "Clave-nueva-9"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_job_de_precios_de_referencia_guarda_y_es_idempotente()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var fecha = new DateOnly(2026, 8, 3);

        PrecioRelevadoDto Relevado(decimal promedio) => new(
            _api.ModeloId, 2019, fecha, "Usd", promedio, promedio - 1_000m, promedio + 1_000m, 42, "MercadoLibre");

        var primera = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-referencia",
            new RegistrarPreciosReferenciaRequest([Relevado(14_000m)]));
        primera.EnsureSuccessStatusCode();

        var segunda = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-referencia",
            new RegistrarPreciosReferenciaRequest([Relevado(14_500m)]));
        segunda.EnsureSuccessStatusCode();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var precios = await db.PreciosReferencia
            .Where(p => p.Fecha == fecha && p.ModeloId == _api.ModeloId)
            .ToListAsync();

        // Una sola fila por modelo, año, moneda, fecha y fuente: el cron puede reintentar
        // sin arruinar la serie histórica.
        Assert.Single(precios);
        Assert.Equal(14_500m, precios[0].Promedio);
        Assert.Equal(42, precios[0].Muestras);
    }

    [Fact]
    public async Task Un_precio_de_referencia_de_un_modelo_inexistente_se_rechaza()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-referencia",
            new RegistrarPreciosReferenciaRequest([
                new PrecioRelevadoDto(999_999, 2019, new DateOnly(2026, 8, 3), "Usd", 10_000m, 9_000m, 11_000m, 5, "MercadoLibre"),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>
    /// Un promedio fuera del rango relevado es un relevamiento mal armado, y guardarlo
    /// contamina la serie para siempre.
    /// </summary>
    [Fact]
    public async Task Un_promedio_fuera_del_rango_se_rechaza()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-referencia",
            new RegistrarPreciosReferenciaRequest([
                new PrecioRelevadoDto(_api.ModeloId, 2019, new DateOnly(2026, 8, 3), "Usd", 50_000m, 9_000m, 11_000m, 5, "MercadoLibre"),
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_job_de_precios_sin_secreto_responde_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/jobs/precios-referencia",
            new RegistrarPreciosReferenciaRequest([
                new PrecioRelevadoDto(_api.ModeloId, 2019, new DateOnly(2026, 8, 3), "Usd", 10_000m, 9_000m, 11_000m, 5, "MercadoLibre"),
            ]));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_job_de_cotizaciones_sin_secreto_responde_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/jobs/cotizaciones",
            new RegistrarCotizacionRequest(new DateOnly(2026, 8, 1), 40.15m));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_job_de_cotizaciones_con_el_secreto_guarda_y_es_idempotente()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);

        var fecha = new DateOnly(2026, 8, 2);

        var primera = await cliente.PostAsJsonAsync(
            "/api/jobs/cotizaciones", new RegistrarCotizacionRequest(fecha, 40.15m));
        primera.EnsureSuccessStatusCode();

        var segunda = await cliente.PostAsJsonAsync(
            "/api/jobs/cotizaciones", new RegistrarCotizacionRequest(fecha, 41.20m));
        segunda.EnsureSuccessStatusCode();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cotizaciones = await db.Cotizaciones.Where(c => c.Fecha == fecha).ToListAsync();

        Assert.Single(cotizaciones);
        Assert.Equal(41.20m, cotizaciones[0].UsdUyu);
    }
}
