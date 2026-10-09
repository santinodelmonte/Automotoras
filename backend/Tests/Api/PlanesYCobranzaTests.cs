using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutomotoraSaaS.Core.Admin;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Users;
using AutomotoraSaaS.Core.Vehiculos;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// Planes, topes, cobranza y suspensión, por el pipeline completo.
/// </summary>
/// <remarks>
/// Cada test arma su propia automotora con <see cref="FabricaDeApi.AutomotoraConPlan"/>:
/// comparten la base, y un test que le cambia el plan o el pago a Norte cambiaría lo que
/// ven todos los demás.
/// </remarks>
public sealed class PlanesYCobranzaTests : IClassFixture<FabricaDeApi>
{
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly FabricaDeApi _api;

    public PlanesYCobranzaTests(FabricaDeApi api)
    {
        _api = api;
    }

    // ------------------------------------------------------------ Paso 3: topes

    [Fact]
    public async Task El_vehiculo_41_en_Vidriera_se_rechaza_nombrando_el_tope_y_el_uso()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("tope-vehiculos", CodigosDePlan.Vidriera);
        Publicar(tenantId, 40, EstadoVehiculo.Disponible);

        using var cliente = await _api.ClienteDeAsync(owner);
        var respuesta = await cliente.PostAsJsonAsync("/api/vehiculos", NuevoVehiculo());

        var problema = await LeerProblemaDelPlan(respuesta);
        Assert.Equal("vehiculos", problema.GetProperty("recurso").GetString());
        Assert.Equal(40, problema.GetProperty("tope").GetInt32());
        Assert.Equal(40, problema.GetProperty("uso").GetInt32());
        Assert.Contains("40", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    /// <summary>Los vendidos y pausados no ocupan lugar en la vidriera, y no cuentan.</summary>
    [Fact]
    public async Task Los_vendidos_y_pausados_no_cuentan_para_el_tope()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("tope-vendidos", CodigosDePlan.Vidriera);
        Publicar(tenantId, 39, EstadoVehiculo.Disponible);
        Publicar(tenantId, 20, EstadoVehiculo.Vendido);
        Publicar(tenantId, 5, EstadoVehiculo.Pausado);

        using var cliente = await _api.ClienteDeAsync(owner);
        var respuesta = await cliente.PostAsJsonAsync("/api/vehiculos", NuevoVehiculo());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Volver_a_publicar_una_unidad_pausada_tambien_respeta_el_tope()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("tope-republicar", CodigosDePlan.Vidriera);
        Publicar(tenantId, 40, EstadoVehiculo.Disponible);
        var pausado = Publicar(tenantId, 1, EstadoVehiculo.Pausado).Single();

        using var cliente = await _api.ClienteDeAsync(owner);

        var aDisponible = await cliente.PostAsJsonAsync(
            $"/api/vehiculos/{pausado}/estado", new CambiarEstadoRequest("Disponible", null, null));
        await LeerProblemaDelPlan(aDisponible);

        // Pasar de disponible a reservado no publica nada nuevo: tiene que andar igual.
        var disponible = Publicar(tenantId, 0, EstadoVehiculo.Disponible, existentes: true).First();
        var aReservado = await cliente.PostAsJsonAsync(
            $"/api/vehiculos/{disponible}/estado", new CambiarEstadoRequest("Reservado", null, null));
        Assert.Equal(HttpStatusCode.OK, aReservado.StatusCode);
    }

    [Fact]
    public async Task El_tercer_usuario_en_Vidriera_se_rechaza()
    {
        var (_, owner) = _api.AutomotoraConPlan("tope-usuarios", CodigosDePlan.Vidriera);

        using var cliente = await _api.ClienteDeAsync(owner);

        var segundo = await cliente.PostAsJsonAsync("/api/users", new CrearUsuarioRequest(
            "segundo@tope-usuarios.uy", "Segundo", "Clave-nueva-9", "Seller"));
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);

        var tercero = await cliente.PostAsJsonAsync("/api/users", new CrearUsuarioRequest(
            "tercero@tope-usuarios.uy", "Tercero", "Clave-nueva-9", "Seller"));

        var problema = await LeerProblemaDelPlan(tercero);
        Assert.Equal("usuarios", problema.GetProperty("recurso").GetString());
        Assert.Equal(2, problema.GetProperty("tope").GetInt32());
    }

    [Fact]
    public async Task Reactivar_un_usuario_por_encima_del_tope_se_rechaza()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("tope-reactivar", CodigosDePlan.Vidriera);

        using var cliente = await _api.ClienteDeAsync(owner);

        var creado = await cliente.PostAsJsonAsync("/api/users", new CrearUsuarioRequest(
            "uno@tope-reactivar.uy", "Uno", "Clave-nueva-9", "Seller"));
        var uno = await creado.Content.ReadFromJsonAsync<UsuarioDto>();

        // Se da de baja (queda un lugar), entra otro, y el primero ya no puede volver.
        await cliente.PutAsJsonAsync($"/api/users/{uno!.Id}", new ActualizarUsuarioRequest("Uno", false));
        await cliente.PostAsJsonAsync("/api/users", new CrearUsuarioRequest(
            "dos@tope-reactivar.uy", "Dos", "Clave-nueva-9", "Seller"));

        var reactivar = await cliente.PutAsJsonAsync($"/api/users/{uno.Id}", new ActualizarUsuarioRequest("Uno", true));

        await LeerProblemaDelPlan(reactivar);
        Assert.True(tenantId > 0);
    }

    [Fact]
    public async Task Un_owner_de_Vidriera_no_accede_a_los_reportes()
    {
        var (_, owner) = _api.AutomotoraConPlan("sin-reportes", CodigosDePlan.Vidriera);

        using var cliente = await _api.ClienteDeAsync(owner);

        foreach (var ruta in new[]
                 {
                     "/api/reportes/demanda", "/api/reportes/busquedas-sin-resultado",
                     "/api/reportes/sugerencias", "/api/reportes/benchmark",
                 })
        {
            await LeerProblemaDelPlan(await cliente.GetAsync(ruta));
        }
    }

    [Fact]
    public async Task Demanda_tiene_reportes_pero_no_benchmark()
    {
        var (_, owner) = _api.AutomotoraConPlan("solo-demanda", CodigosDePlan.Demanda);

        using var cliente = await _api.ClienteDeAsync(owner);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/reportes/demanda")).StatusCode);

        var benchmark = await LeerProblemaDelPlan(await cliente.GetAsync("/api/reportes/benchmark"));
        Assert.Equal("benchmark", benchmark.GetProperty("recurso").GetString());
    }

    /// <summary>
    /// Bajar de plan por encima del tope no rompe nada: lo publicado sigue publicado y se
    /// bloquea publicar más.
    /// </summary>
    [Fact]
    public async Task Bajar_de_plan_por_encima_del_tope_no_borra_nada_y_bloquea_publicar_mas()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("baja-de-plan", CodigosDePlan.Full);
        Publicar(tenantId, 3, EstadoVehiculo.Disponible);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var mini = await admin.PostAsJsonAsync("/api/admin/planes", new GuardarPlanRequest(
            "mini-pruebas", "Mini", 1_000m, "Uyu", MaxVehiculos: 2, MaxUsuarios: 5,
            IncluyeReportes: false, IncluyeBenchmark: false, IncluyeDominioPropio: false,
            HorasSoporteMes: 0, Activo: true));
        Assert.Equal(HttpStatusCode.Created, mini.StatusCode);

        var cambio = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/suscripcion", new CambiarPlanRequest("mini-pruebas"));
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);

        var planes = await admin.GetFromJsonAsync<List<PlanDto>>("/api/admin/planes") ?? [];
        Assert.Contains(planes, p => p.Codigo == "mini-pruebas");
        Assert.Equal(planes.OrderBy(p => p.PrecioMensual).Select(p => p.Id), planes.Select(p => p.Id));

        using var cliente = await _api.ClienteDeAsync(owner);

        var listado = await cliente.GetFromJsonAsync<JsonElement>("/api/vehiculos");
        Assert.Equal(3, listado.GetProperty("total").GetInt32());

        await LeerProblemaDelPlan(await cliente.PostAsJsonAsync("/api/vehiculos", NuevoVehiculo()));

        var plan = await cliente.GetFromJsonAsync<SituacionDelPlanDto>("/api/tenant/plan");
        Assert.Equal(3, plan!.Vehiculos.Usados);
        Assert.Equal(2, plan.Vehiculos.Tope);
        Assert.True(plan.Vehiculos.CercaDelTope);
    }

    [Fact]
    public async Task Crear_una_automotora_con_dominio_en_un_plan_que_no_lo_incluye_se_rechaza()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var respuesta = await admin.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "vidriera-con-dominio", "Con dominio", "vidrieracondominio.uy",
            "owner@vidriera-con-dominio.uy", "Owner", "Clave-nueva-9", CodigosDePlan.Vidriera));

        var problema = await LeerProblemaDelPlan(respuesta);
        Assert.Equal("dominio-propio", problema.GetProperty("recurso").GetString());
    }

    // ------------------------------------------------------------ Paso 2: cobranza

    [Theory]
    [InlineData(FabricaDeApi.EmailOwnerNorte)]
    [InlineData(FabricaDeApi.EmailVendedorNorte)]
    public async Task Los_endpoints_de_cobranza_no_son_accesibles_para_owner_ni_seller(string email)
    {
        using var cliente = await _api.ClienteDeAsync(email);
        var norte = _api.TenantNorte;

        var respuestas = new[]
        {
            await cliente.GetAsync("/api/admin/cobranza"),
            await cliente.GetAsync("/api/admin/planes"),
            await cliente.GetAsync($"/api/admin/tenants/{norte}/suscripcion"),
            await cliente.PostAsJsonAsync($"/api/admin/tenants/{norte}/suscripcion", new CambiarPlanRequest("vidriera")),
            await cliente.PostAsJsonAsync($"/api/admin/tenants/{norte}/pagos",
                new RegistrarPagoRequest(Hoy, 1m, null, Hoy.AddYears(5), "Transferencia")),
            await cliente.PostAsJsonAsync($"/api/admin/tenants/{norte}/baja", new DarDeBajaRequest(Hoy, "x")),
            await cliente.PostAsJsonAsync("/api/admin/planes", new GuardarPlanRequest(
                "gratis", "Gratis", 0m, "Uyu", null, null, true, true, true, 0, true)),
        };

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task El_tablero_de_cobranza_muestra_primero_a_los_suspendidos_y_despues_a_los_que_vencen()
    {
        var (suspendida, _) = _api.AutomotoraConPlan("cobranza-suspendida", CodigosDePlan.Demanda, Hoy.AddDays(-30));
        var (porVencer, _) = _api.AutomotoraConPlan("cobranza-por-vencer", CodigosDePlan.Demanda, Hoy.AddDays(3));
        var (enGracia, _) = _api.AutomotoraConPlan("cobranza-en-gracia", CodigosDePlan.Demanda, Hoy.AddDays(-2));

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var filas = await admin.GetFromJsonAsync<List<FilaDeCobranzaDto>>("/api/admin/cobranza");

        var posicion = (int id) => filas!.FindIndex(f => f.TenantId == id);

        Assert.Equal("Suspendido", filas![posicion(suspendida)].Estado);
        Assert.Equal("Gracia", filas[posicion(enGracia)].Estado);
        Assert.Equal("PorVencer", filas[posicion(porVencer)].Estado);
        Assert.Equal(3, filas[posicion(porVencer)].DiasParaVencer);

        Assert.True(posicion(suspendida) < posicion(enGracia));
        Assert.True(posicion(enGracia) < posicion(porVencer));
        Assert.True(posicion(porVencer) < posicion(_api.TenantNorte));
    }

    [Fact]
    public async Task Cambiar_de_plan_cierra_la_vigente_y_respeta_lo_ya_pagado()
    {
        var pagaHasta = Hoy.AddDays(20);
        var (tenantId, _) = _api.AutomotoraConPlan("cambio-de-plan", CodigosDePlan.Vidriera, pagaHasta);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var respuesta = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/suscripcion", new CambiarPlanRequest("Full"));

        var suscripcion = await respuesta.Content.ReadFromJsonAsync<SuscripcionDeTenantDto>();

        Assert.Equal(CodigosDePlan.Full, suscripcion!.Vigente!.Plan.Codigo);
        Assert.Equal(pagaHasta, suscripcion.Vigente.PagaHasta);
        Assert.Equal(2, suscripcion.Historial.Count);
        Assert.Single(suscripcion.Historial, s => s.Fin is null);

        var repetido = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/suscripcion", new CambiarPlanRequest("full"));
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
    }

    [Fact]
    public async Task Registrar_un_pago_empuja_el_vencimiento_desde_el_dia_siguiente_al_ultimo_pago()
    {
        var pagaHasta = Hoy.AddDays(2);
        var (tenantId, _) = _api.AutomotoraConPlan("pago-normal", CodigosDePlan.Demanda, pagaHasta);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var respuesta = await admin.PostAsJsonAsync($"/api/admin/tenants/{tenantId}/pagos",
            new RegistrarPagoRequest(Hoy, 6_900m, null, pagaHasta.AddMonths(1), "Transferencia", Comprobante: "TRF-123"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var suscripcion = await respuesta.Content.ReadFromJsonAsync<SuscripcionDeTenantDto>();

        Assert.Equal(pagaHasta.AddMonths(1), suscripcion!.Vigente!.PagaHasta);
        Assert.Equal("Vigente", suscripcion.Situacion.Estado);

        var pago = Assert.Single(suscripcion.Pagos);
        Assert.Equal(pagaHasta.AddDays(1), pago.PeriodoDesde);
        Assert.Equal("Uyu", pago.Moneda);
    }

    [Fact]
    public async Task Un_pago_de_un_periodo_viejo_no_acorta_la_cobertura()
    {
        var pagaHasta = Hoy.AddMonths(2);
        var (tenantId, _) = _api.AutomotoraConPlan("pago-viejo", CodigosDePlan.Demanda, pagaHasta);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var respuesta = await admin.PostAsJsonAsync($"/api/admin/tenants/{tenantId}/pagos",
            new RegistrarPagoRequest(Hoy, 6_900m, Hoy.AddMonths(-1), Hoy, "Efectivo"));

        var suscripcion = await respuesta.Content.ReadFromJsonAsync<SuscripcionDeTenantDto>();
        Assert.Equal(pagaHasta, suscripcion!.Vigente!.PagaHasta);
    }

    [Fact]
    public async Task Un_superadmin_da_de_alta_asigna_plan_cobra_y_ve_el_vencimiento_sin_tocar_la_base()
    {
        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);

        var alta = await admin.PostAsJsonAsync("/api/admin/tenants", new CrearTenantRequest(
            "circuito-completo", "Circuito completo", null, "owner@circuito-completo.uy", "Owner",
            "Clave-nueva-9", CodigosDePlan.Vidriera));
        var tenant = await alta.Content.ReadFromJsonAsync<TenantAdminDto>();

        await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenant!.Id}/suscripcion", new CambiarPlanRequest(CodigosDePlan.Demanda));

        var bonificadoHasta = Suscripciones.UltimoDiaCubierto(Hoy, Suscripciones.MesesBonificados);
        await admin.PostAsJsonAsync($"/api/admin/tenants/{tenant.Id}/pagos",
            new RegistrarPagoRequest(Hoy, 6_900m, null, bonificadoHasta.AddMonths(1), "Transferencia"));

        var filas = await admin.GetFromJsonAsync<List<FilaDeCobranzaDto>>("/api/admin/cobranza");
        var fila = filas!.Single(f => f.TenantId == tenant.Id);

        Assert.Equal("Demanda", fila.Plan);
        Assert.Equal(bonificadoHasta.AddMonths(1), fila.PagaHasta);
        Assert.Equal("Vigente", fila.Estado);
    }

    // ------------------------------------------------------------ Paso 4: suspensión

    [Fact]
    public async Task En_gracia_el_sitio_publico_sigue_arriba()
    {
        _api.AutomotoraConPlan("en-gracia", CodigosDePlan.Demanda, Hoy.AddDays(-10));

        using var cliente = _api.CreateClient();
        var respuesta = await cliente.GetAsync("/t/en-gracia/api/public/tenant");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Suspendido_el_sitio_publico_responde_mantenimiento_y_el_panel_sigue_andando()
    {
        var (_, owner) = _api.AutomotoraConPlan("suspendida", CodigosDePlan.Demanda, Hoy.AddDays(-11));

        using var publico = _api.CreateClient();
        var sitio = await publico.GetAsync("/t/suspendida/api/public/vehiculos");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, sitio.StatusCode);
        Assert.True(sitio.Headers.RetryAfter is not null);

        var problema = await sitio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sitio-en-mantenimiento", problema.GetProperty("type").GetString());
        Assert.Equal("Automotora suspendida", problema.GetProperty("automotora").GetString());

        using var panel = await _api.ClienteDeAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await panel.GetAsync("/api/vehiculos")).StatusCode);

        var plan = await panel.GetFromJsonAsync<SituacionDelPlanDto>("/api/tenant/plan");
        Assert.Equal("Suspendido", plan!.Estado);
        Assert.Equal(-11, plan.DiasParaVencer);
    }

    [Fact]
    public async Task Registrar_un_pago_reactiva_el_sitio_sin_pasos_manuales()
    {
        var (tenantId, _) = _api.AutomotoraConPlan("reactivada", CodigosDePlan.Demanda, Hoy.AddDays(-40));

        using var publico = _api.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await publico.GetAsync("/t/reactivada/api/public/tenant")).StatusCode);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        await admin.PostAsJsonAsync($"/api/admin/tenants/{tenantId}/pagos",
            new RegistrarPagoRequest(Hoy, 6_900m, Hoy.AddDays(-39), Hoy.AddMonths(1), "Transferencia"));

        Assert.Equal(HttpStatusCode.OK, (await publico.GetAsync("/t/reactivada/api/public/tenant")).StatusCode);
    }

    [Fact]
    public async Task La_baja_apaga_el_sitio_pero_deja_entrar_al_panel()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("dada-de-baja", CodigosDePlan.Full);

        using var admin = await _api.ClienteDeAsync(FabricaDeApi.EmailSuperAdmin);
        var baja = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/baja", new DarDeBajaRequest(Hoy, "Cierra el local."));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);

        var futura = await admin.PostAsJsonAsync(
            $"/api/admin/tenants/{tenantId}/baja", new DarDeBajaRequest(Hoy, "otra vez"));
        Assert.Equal(HttpStatusCode.Conflict, futura.StatusCode);

        using var publico = _api.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await publico.GetAsync("/t/dada-de-baja/api/public/tenant")).StatusCode);

        using var panel = await _api.ClienteDeAsync(owner);
        Assert.Equal(HttpStatusCode.OK, (await panel.GetAsync("/api/vehiculos")).StatusCode);
    }

    // ------------------------------------------------------------ Paso 4: avisos

    [Fact]
    public async Task El_job_de_avisos_sin_secreto_responde_401()
    {
        using var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsync("/api/jobs/avisos-de-vencimiento", null);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cada_etapa_de_un_vencimiento_se_avisa_una_sola_vez()
    {
        var (tenantId, owner) = _api.AutomotoraConPlan("aviso-por-vencer", CodigosDePlan.Demanda, Hoy.AddDays(5));
        var (_, alDia) = _api.AutomotoraConPlan("aviso-al-dia", CodigosDePlan.Demanda, Hoy.AddMonths(2));

        using var cron = ClienteDelCron();

        Assert.Equal(HttpStatusCode.OK, (await cron.PostAsync("/api/jobs/avisos-de-vencimiento", null)).StatusCode);
        await cron.PostAsync("/api/jobs/avisos-de-vencimiento", null);

        var aviso = Assert.Single(_api.Correo.Para(owner));
        Assert.Contains("vence", aviso.Asunto, StringComparison.Ordinal);
        Assert.Empty(_api.Correo.Para(alDia));

        // Se atrasa: la etapa de gracia es otro aviso.
        _api.ConLaBase(db => db.Suscripciones.IgnoreQueryFilters()
            .Single(s => s.TenantId == tenantId && s.Fin == null).PagaHasta = Hoy.AddDays(-3));

        await cron.PostAsync("/api/jobs/avisos-de-vencimiento", null);

        Assert.Equal(2, _api.Correo.Para(owner).Count);
        Assert.Contains("vencido", _api.Correo.Para(owner)[1].Asunto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_aviso_que_no_sale_se_reintenta_en_la_proxima_corrida()
    {
        var (_, owner) = _api.AutomotoraConPlan("aviso-fallido", CodigosDePlan.Demanda, Hoy.AddDays(-20));
        _api.Correo.Fallan[owner] = true;

        using var cron = ClienteDelCron();

        var primera = await (await cron.PostAsync("/api/jobs/avisos-de-vencimiento", null))
            .Content.ReadFromJsonAsync<ResultadoDeAvisosDto>();
        Assert.True(primera!.Fallidos >= 1);
        Assert.Empty(_api.Correo.Para(owner));

        _api.Correo.Fallan.TryRemove(owner, out _);
        await cron.PostAsync("/api/jobs/avisos-de-vencimiento", null);

        var aviso = Assert.Single(_api.Correo.Para(owner));
        Assert.Contains("mantenimiento", aviso.Asunto, StringComparison.Ordinal);
    }

    private HttpClient ClienteDelCron()
    {
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Job-Secret", FabricaDeApi.SecretoDeJobs);
        return cliente;
    }

    // ------------------------------------------------------------ Ayudas

    private static async Task<JsonElement> LeerProblemaDelPlan(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("limite-del-plan", problema.GetProperty("type").GetString());

        return problema;
    }

    /// <summary>
    /// Carga <paramref name="cantidad"/> vehículos en el estado pedido, directo en la base.
    /// Con <paramref name="existentes"/>, en vez de crear devuelve los ids de los que ya
    /// tiene en ese estado.
    /// </summary>
    private List<int> Publicar(int tenantId, int cantidad, EstadoVehiculo estado, bool existentes = false)
    {
        var ids = new List<int>();

        _api.ConLaBase(db =>
        {
            if (existentes)
            {
                ids.AddRange(db.Vehiculos.IgnoreQueryFilters()
                    .Where(v => v.TenantId == tenantId && v.Estado == estado)
                    .Select(v => v.Id));
                return;
            }

            var nuevos = Enumerable.Range(0, cantidad).Select(_ => new Vehiculo
            {
                TenantId = tenantId,
                ModeloId = _api.ModeloId,
                Anio = 2019,
                Kilometraje = 50_000,
                Combustible = Combustible.Nafta,
                Transmision = Transmision.Manual,
                Precio = 15_000m,
                Moneda = Moneda.Usd,
                Estado = estado,
                FechaPublicacion = DateTime.UtcNow.AddDays(-10),
            }).ToList();

            db.Vehiculos.AddRange(nuevos);
            db.SaveChanges();
            ids.AddRange(nuevos.Select(v => v.Id));
        });

        return ids;
    }

    private GuardarVehiculoRequest NuevoVehiculo() => new(
        _api.ModeloId, null, 2020, 30_000, "Nafta", "Manual", null, null, null,
        18_000m, "Usd", null, false, null, null);
}
