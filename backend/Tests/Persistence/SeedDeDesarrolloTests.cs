using System.Runtime.CompilerServices;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Vehiculos;
using AutomotoraSaaS.Core.Reportes;
using AutomotoraSaaS.Infrastructure.Auth;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Infrastructure.Persistence;
using AutomotoraSaaS.Infrastructure.Planes;
using AutomotoraSaaS.Infrastructure.Vehiculos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// Lo que el seed de desarrollo tiene que producir para que las pantallas se puedan mirar.
/// </summary>
/// <remarks>
/// No prueban lógica de negocio —esa está probada en otro lado— sino que los datos de
/// desarrollo alcancen para que los reportes digan algo. Es el tipo de cosa que se rompe
/// sin ruido: bajar las automotoras del seed de siete a tres no falla ninguna compilación
/// ni ningún otro test, y deja el benchmark apagado hasta que alguien lo abre y ve el
/// cartel de muestra insuficiente.
/// </remarks>
public sealed class SeedDeDesarrolloTests : IDisposable
{
    private readonly SqliteConnection _conexion;
    private readonly AppDbContext _db;

    public SeedDeDesarrolloTests()
    {
        _conexion = new SqliteConnection("Filename=:memory:");
        _conexion.Open();

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexion)
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new AppDbContext(opciones, new TenantContext(), TimeProvider.System);
        _db.Database.EnsureCreated();

        SeedDeDesarrollo.EjecutarAsync(
                _db,
                new PasswordHasherPbkdf2(),
                TimeProvider.System,
                "una-contrasena-de-prueba")
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public void Siembra_automotoras_suficientes_para_que_el_benchmark_publique()
    {
        var automotoras = _db.Tenants.IgnoreQueryFilters().Count(t => t.Activo);

        // La que pregunta no se cuenta a sí misma: hacen falta las mínimas más una.
        Assert.True(
            automotoras > ReglasDelBenchmark.AutomotorasMinimas,
            $"El benchmark necesita más de {ReglasDelBenchmark.AutomotorasMinimas} automotoras activas y el seed dejó {automotoras}.");
    }

    [Fact]
    public void Cada_automotora_queda_con_una_suscripcion_vigente()
    {
        var tenants = _db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToList();

        var vigentes = _db.Suscripciones
            .IgnoreQueryFilters()
            .Where(s => s.Fin == null)
            .Select(s => s.TenantId)
            .ToList();

        Assert.Equal(tenants.Order(), vigentes.Order());
    }

    [Fact]
    public void Cada_automotora_aporta_al_benchmark()
    {
        var porTenant = _db.Vehiculos
            .IgnoreQueryFilters()
            .GroupBy(v => v.TenantId)
            .Select(g => new { g.Key, Cuantos = g.Count() })
            .ToList();

        // Assert.All sobre una lista vacía pasa sin probar nada: si el seed no sembrara un
        // solo vehículo, este test seguiría en verde.
        Assert.NotEmpty(porTenant);

        Assert.All(porTenant, x => Assert.True(
            x.Cuantos >= ReglasDelBenchmark.VehiculosMinimosParaAportar,
            $"La automotora {x.Key} quedó con {x.Cuantos} vehículos y no entra en la muestra."));
    }

    [Fact]
    public void Todo_vehiculo_tiene_fotos_con_portada_y_miniatura()
    {
        var fotos = _db.VehiculoFotos.IgnoreQueryFilters().ToList();
        var vehiculos = _db.Vehiculos.IgnoreQueryFilters().Select(v => v.Id).ToList();

        Assert.NotEmpty(fotos);
        Assert.NotEmpty(vehiculos);
        Assert.All(vehiculos, id => Assert.Contains(fotos, f => f.VehiculoId == id && f.EsPortada));

        // La grilla sirve la miniatura; sin ella cae en la imagen de ficha y el listado
        // baja varios megabytes para pintar unas estampillas.
        Assert.All(fotos, f => Assert.False(string.IsNullOrWhiteSpace(f.UrlThumb)));
    }

    [Fact]
    public void Lo_publicado_tiene_precio_de_referencia_de_mercado()
    {
        var publicados = _db.Vehiculos
            .IgnoreQueryFilters()
            .Where(v => v.Estado == EstadoVehiculo.Disponible || v.Estado == EstadoVehiculo.Reservado)
            .Select(v => new { v.ModeloId, v.Anio })
            .Distinct()
            .ToList();

        var snapshots = _db.PreciosDeMercado
            .Select(p => new { p.ModeloId, p.Anio })
            .Distinct()
            .ToList();

        Assert.NotEmpty(publicados);
        Assert.NotEmpty(snapshots);

        Assert.All(publicados, p => Assert.Contains(snapshots, s => s.ModeloId == p.ModeloId && s.Anio == p.Anio));
    }

    [Fact]
    public void La_demanda_sembrada_no_es_toda_igual()
    {
        var eventos = _db.Eventos.IgnoreQueryFilters().ToList();

        var vistasPorVehiculo = eventos
            .Where(e => e.Tipo == TipoEvento.ViewFicha && e.VehiculoId is not null)
            .GroupBy(e => e.VehiculoId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var consultasPorVehiculo = eventos
            .Where(e => e.Tipo is TipoEvento.ClickWhatsapp or TipoEvento.ClickTelefono && e.VehiculoId is not null)
            .GroupBy(e => e.VehiculoId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var senales = _db.Vehiculos
            .IgnoreQueryFilters()
            .ToList()
            .Select(v => UmbralesDeDemanda.Clasificar(
                vistasPorVehiculo.GetValueOrDefault(v.Id),
                consultasPorVehiculo.GetValueOrDefault(v.Id),
                (int)(DateTime.UtcNow - v.FechaPublicacion).TotalDays))
            .Distinct()
            .ToList();

        // Con una sola señal en todo el stock, el reporte de demanda no se puede juzgar:
        // se ve una columna entera diciendo lo mismo y no hay forma de saber si clasifica.
        Assert.True(
            senales.Count >= 3,
            $"El stock sembrado produce {senales.Count} señal(es) distinta(s): {string.Join(", ", senales)}.");
    }

    /// <summary>
    /// Cada estado del ciclo de cobro tiene al menos una automotora: si no, la pantalla de
    /// cobranza, los topes y la página de mantenimiento no se pueden mirar en desarrollo.
    /// </summary>
    [Fact]
    public void Siembra_una_automotora_en_cada_estado_de_cobro()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var opciones = new OpcionesDeCobranza();

        var estados = _db.Suscripciones
            .IgnoreQueryFilters()
            .Where(s => s.Fin == null)
            .Select(s => new { s.Tenant!.Slug, Plan = s.Plan!.Codigo, s.PagaHasta })
            .AsEnumerable()
            .ToDictionary(s => s.Slug, s => (s.Plan, Estado: CicloDeCobro.Evaluar(s.PagaHasta, hoy, opciones)));

        Assert.Equal((CodigosDePlan.Full, EstadoDeCobro.Vigente), estados["norte"]);
        Assert.Equal((CodigosDePlan.Demanda, EstadoDeCobro.PorVencer), estados["sur"]);
        Assert.Equal((CodigosDePlan.Vidriera, EstadoDeCobro.Gracia), estados["costa"]);
        Assert.Equal((CodigosDePlan.Demanda, EstadoDeCobro.Suspendido), estados["litoral"]);

        Assert.True(_db.Users.IgnoreQueryFilters()
            .Single(u => u.Email == SeedDeCobranza.EmailConPasswordProvisoria).DebeCambiarPassword);
    }

    /// <summary>El seed corre en cada arranque: correrlo de nuevo no puede duplicar cobros ni romper nada.</summary>
    [Fact]
    public async Task Correr_el_seed_de_nuevo_no_cambia_la_cobranza()
    {
        var pagos = _db.Pagos.IgnoreQueryFilters().Count();
        var suscripciones = _db.Suscripciones.IgnoreQueryFilters().Count();

        await SeedDeDesarrollo.EjecutarAsync(_db, new PasswordHasherPbkdf2(), TimeProvider.System, "una-contrasena-de-prueba");

        Assert.Equal(pagos, _db.Pagos.IgnoreQueryFilters().Count());
        Assert.Equal(suscripciones, _db.Suscripciones.IgnoreQueryFilters().Count());
    }

    /// <summary>
    /// Los CSV de ejemplo de <c>docs/ejemplos</c> se validan contra el catálogo del seed: si
    /// alguien cambia el catálogo, el ejemplo no puede quedar roto sin que nadie se entere.
    /// </summary>
    [Fact]
    public async Task Los_csv_de_ejemplo_validan_contra_el_catalogo_del_seed()
    {
        var norte = _db.Tenants.Single(t => t.Slug == "norte").Id;
        var importador = new ImportadorDeStock(
            _db,
            new GuardarVehiculoRequestValidator(TimeProvider.System),
            new PoliticaDePlanEnBase(_db, TimeProvider.System, Options.Create(new OpcionesDeCobranza())),
            TimeProvider.System);

        var bueno = await importador.ProcesarAsync(norte, Ejemplo("stock-de-ejemplo.csv"), confirmar: false, cargarCostos: true);
        Assert.Empty(bueno.Errores);
        Assert.Equal(8, bueno.Validas);

        var malo = await importador.ProcesarAsync(norte, Ejemplo("stock-con-errores.csv"), confirmar: false, cargarCostos: true);
        Assert.Equal(1, malo.Validas);
        Assert.Equal([3, 4, 5, 6, 7], malo.Errores.Select(e => e.Fila).Distinct().Order());
    }

    // Se busca desde los binarios y, si se compiló con --artifacts-path fuera del repo,
    // desde este mismo archivo fuente.
    private static string Ejemplo(string nombre, [CallerFilePath] string fuente = "")
    {
        var raiz = RaizDelRepo(AppContext.BaseDirectory) ?? RaizDelRepo(Path.GetDirectoryName(fuente))
            ?? throw new InvalidOperationException("No se encontró la raíz del repo.");

        return File.ReadAllText(Path.Combine(raiz, "docs", "ejemplos", nombre));
    }

    private static string? RaizDelRepo(string? desde)
    {
        var carpeta = string.IsNullOrEmpty(desde) ? null : new DirectoryInfo(desde);

        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "AutomotoraSaaS.sln")))
        {
            carpeta = carpeta.Parent;
        }

        return carpeta?.FullName;
    }

    public void Dispose()
    {
        _db.Dispose();
        _conexion.Dispose();
    }
}
