using System.Net;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Dashboard;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
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

    /// <summary>
    /// De la visita no se guarda ni la IP, ni el navegador, ni la página de origen: la
    /// tabla no tiene dónde. Este test lee la fila cruda para que nadie vuelva a agregarlos
    /// sin enterarse.
    /// </summary>
    [Fact]
    public async Task El_evento_no_guarda_datos_de_quien_visita()
    {
        using var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("User-Agent", "Navegador-De-Prueba/1.0");
        cliente.DefaultRequestHeaders.Add("Referer", "https://ejemplo.uy/?email=alguien@ejemplo.uy");

        await cliente.PostAsJsonAsync(
            "/t/norte/api/public/events",
            new RegistrarEventoRequest("ClickWhatsapp", _api.VehiculoDeNorte, "sesion-ip"));

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var evento = await db.Eventos
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.SessionId == "sesion-ip");

        Assert.NotNull(evento);

        var columnas = db.Model.FindEntityType(typeof(Evento))!
            .GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        Assert.Equal(
            ["created_at", "id", "metadata", "session_id", "tenant_id", "tipo", "vehiculo_id"],
            columnas.Order(StringComparer.Ordinal));
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
    /// El SuperAdmin no tiene automotora, pero el catálogo lo administra él: sin las
    /// carrocerías, el alta de modelos no puede ofrecer ninguna.
    /// </summary>
    [Fact]
    public async Task El_superadmin_lee_las_carrocerias_para_el_alta_de_modelos()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var carrocerias = await cliente.GetFromJsonAsync<List<string>>("/api/admin/catalogo/carrocerias");

        Assert.NotNull(carrocerias);
        Assert.Contains(nameof(Carroceria.Sedan), carrocerias);
    }

    [Fact]
    public async Task El_owner_no_lee_las_carrocerias_del_catalogo_de_superadmin()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailOwnerNorte);

        var respuesta = await cliente.GetAsync("/api/admin/catalogo/carrocerias");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
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
            "este", "Automotora Este", null, "owner@este.uy", "Owner Este", "Clave-nueva-9"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var sesion = await _api.LoginAsync("owner@este.uy", "Clave-nueva-9");

        Assert.Equal("Owner", sesion.Usuario.Rol);
        Assert.NotNull(sesion.Usuario.TenantId);
        Assert.NotEqual(_api.TenantNorte, sesion.Usuario.TenantId);
    }

    /// <summary>
    /// Ninguna automotora existe sin plan: el alta la deja suscripta, con los meses
    /// bonificados ya registrados.
    /// </summary>
    [Theory]
    [InlineData(null, CodigosDePlan.PorDefecto)]
    [InlineData("Full", CodigosDePlan.Full)]
    public async Task El_alta_deja_a_la_automotora_con_una_suscripcion_vigente(string? plan, string esperado)
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var slug = plan is null ? "plan-por-defecto" : "plan-pedido";

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            slug, "Automotora con plan", null, $"owner@{slug}.uy", "Owner", "Clave-nueva-9", plan));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = await respuesta.Content.ReadFromJsonAsync<TenantAdminDto>();

        _api.ConLaBase(db =>
        {
            var suscripcion = db.Suscripciones
                .IgnoreQueryFilters()
                .Include(s => s.Plan)
                .Include(s => s.Pagos)
                .Single(s => s.TenantId == creada!.Id && s.Fin == null);

            Assert.Equal(esperado, suscripcion.Plan!.Codigo);
            Assert.True(suscripcion.PagaHasta > suscripcion.Inicio);
            Assert.Equal(0m, Assert.Single(suscripcion.Pagos).Monto);
        });
    }

    [Fact]
    public async Task El_alta_puede_traer_la_identidad_y_el_contacto_y_el_sitio_sale_con_su_marca()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "con-marca", "Con marca", null, "owner@con-marca.uy", "Owner", "Clave-nueva-9", null,
            ColorPrimario: "#DC2626", ColorSecundario: "#0F172A", Whatsapp: "+59899123456",
            Telefono: "+598 2 400 1234", Direccion: "Av. Italia 1234"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        using var publico = _api.CreateClient();
        var sitio = await publico.GetFromJsonAsync<TenantPublicoDto>("/t/con-marca/api/public/tenant");

        Assert.Equal("#dc2626", sitio!.ColorPrimario);
        Assert.Equal("+59899123456", sitio.Whatsapp);
        Assert.Equal("Av. Italia 1234", sitio.Direccion);
    }

    [Fact]
    public async Task El_alta_con_un_color_mal_escrito_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "color-malo", "Color malo", null, "owner@color-malo.uy", "Owner", "Clave-nueva-9", null,
            ColorPrimario: "rojo"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_alta_con_un_plan_que_no_existe_se_rechaza_y_no_crea_nada()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "sin-plan", "Sin plan", null, "owner@sin-plan.uy", "Owner", "Clave-nueva-9", "platino"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        _api.ConLaBase(db => Assert.False(db.Tenants.Any(t => t.Slug == "sin-plan")));
    }

    [Fact]
    public async Task Un_slug_repetido_se_rechaza()
    {
        using var cliente = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await cliente.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "norte", "Otra Norte", null, "otro@norte.uy", "Otro", "Clave-nueva-9"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
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
