using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Core.Tenants;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>
/// La API entera levantada en memoria, contra SQLite, con dos automotoras cargadas.
/// </summary>
/// <remarks>
/// Los tests de aislamiento tienen que pasar por el pipeline completo —autenticación,
/// resolución de tenant, autorización, filtros globales— porque es ahí donde puede
/// romperse. Probar el <c>DbContext</c> por separado no dice nada sobre lo que responde
/// un endpoint cuando alguien manipula un id en la URL.
/// <para>
/// SQLite y no el proveedor InMemory, por lo mismo que en los tests de persistencia: el
/// InMemory no traduce a SQL y un filtro roto podría pasar igual.
/// </para>
/// </remarks>
public sealed class FabricaDeApi : WebApplicationFactory<Program>
{
    public const string Password = "Prueba-segura-1";

    private readonly SqliteConnection _conexion = new("Filename=:memory:");

    /// <summary>
    /// La configuración de los tests, puesta en variables de entorno antes de que se
    /// construya el primer host.
    /// </summary>
    /// <remarks>
    /// No alcanza con <c>ConfigureAppConfiguration</c>: esos delegados corren recién
    /// dentro de <c>builder.Build()</c>, y <c>Program</c> lee y valida el JWT antes, al
    /// registrar la autenticación. Con la config puesta ahí, el <c>JwtBearer</c> quedaba
    /// validando contra el issuer vacío de <c>appsettings.json</c> mientras el generador
    /// firmaba con el de los tests, y todo request autenticado respondía 401.
    /// <para>
    /// Las variables de entorno sí llegan a tiempo, porque <c>Program</c> las agrega
    /// antes de leer nada. Son del proceso entero, y está bien: todos los tests de API
    /// quieren exactamente estos valores.
    /// </para>
    /// </remarks>
    static FabricaDeApi()
    {
        var configuracion = new Dictionary<string, string?>
        {
            // Presente pero sin usar: el DbContext se reemplaza más abajo por SQLite.
            ["ConnectionStrings__Default"] = "Server=no-se-usa;Database=no-se-usa;User Id=no;Password=no;",
            ["Jwt__Issuer"] = "automotora-saas-tests",
            ["Jwt__Audience"] = "automotora-saas-tests",
            ["Jwt__Secret"] = "secreto-de-tests-largo-y-aburrido-de-sobra-32",
            ["Jwt__AccessTokenMinutes"] = "15",
            ["Jwt__RefreshTokenDays"] = "30",
            ["Cors__AllowedOrigins__0"] = "http://localhost:5173",
            ["Jobs__Secret"] = SecretoDeJobs,
            ["Analytics__IpHashSalt"] = "sal-de-tests-estable",
            ["Deploy__IpsPublicas__0"] = IpDeLaAplicacion,

            // El seed de arranque no corre fuera de Development, pero si alguien hereda
            // una variable de su shell, que no se cuele en la base de los tests.
            ["Seed__Password"] = null,
        };

        foreach (var (clave, valor) in configuracion)
        {
            Environment.SetEnvironmentVariable(clave, valor);
        }
    }

    public FabricaDeApi()
    {
        // Abierta durante toda la vida de la fábrica: una base SQLite en memoria vive
        // mientras haya una conexión abierta, y si se cierra desaparece el esquema.
        _conexion.Open();

        // El esquema y los datos se arman en el constructor, no en IAsyncLifetime: xUnit 2
        // pide un DisposeAsync que devuelve Task y WebApplicationFactory ya trae uno que
        // devuelve ValueTask, y la implementación explícita que hace falta para convivir
        // con las dos no aporta nada que este constructor no resuelva.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        db.Database.EnsureCreated();
        Sembrar(db, hasher);
    }

    public int TenantNorte { get; private set; }
    public int TenantSur { get; private set; }

    public int OwnerDeNorte { get; private set; }
    public int OwnerDeSur { get; private set; }
    public int VendedorDeNorte { get; private set; }
    public int InactivoDeNorte { get; private set; }

    public int MarcaId { get; private set; }
    public int ModeloId { get; private set; }
    public int ModeloDadoDeBaja { get; private set; }

    /// <summary>Disponible, y por lo tanto visible en el sitio público de Norte.</summary>
    public int VehiculoDeNorte { get; private set; }

    /// <summary>Vendido: sigue en la base y no sale en el sitio público.</summary>
    public int VendidoDeNorte { get; private set; }

    /// <summary>
    /// Publicado hace meses y sin un solo evento: el caso de la unidad estancada.
    /// </summary>
    /// <remarks>
    /// Tiene el suyo propio y no comparte el de los demás tests porque su condición es
    /// justamente la ausencia de eventos, y cualquier test que le agregara uno al vehículo
    /// compartido lo cambiaría de señal.
    /// </remarks>
    public int OlvidadoDeNorte { get; private set; }

    public int VehiculoDeSur { get; private set; }

    /// <summary>Storage en memoria. Los tests no tocan el disco ni salen a la red.</summary>
    public AlmacenamientoDePrueba Almacenamiento { get; } = new();

    /// <summary>DNS de mentira: los tests declaran a dónde apunta cada dominio.</summary>
    public DnsDePrueba Dns { get; } = new();

    /// <summary>Correo en memoria: los avisos quedan acá y no salen a ningún lado.</summary>
    public CorreoDePrueba Correo { get; } = new();

    /// <summary>La IP que la configuración de los tests declara como propia.</summary>
    public const string IpDeLaAplicacion = "190.64.10.20";

    public const string EmailOwnerNorte = "owner@norte.uy";
    public const string EmailOwnerSur = "owner@sur.uy";
    public const string EmailVendedorNorte = "vendedor@norte.uy";
    public const string EmailInactivoNorte = "baja@norte.uy";
    public const string EmailSuperAdmin = "super@saas.uy";

    public const string DominioDeNorte = "automotoranorte.uy";

    /// <summary>Cargado pero sin verificar: no sirve el sitio de nadie.</summary>
    public const string DominioSinVerificarDeSur = "automotorasur.uy";

    public const string SecretoDeJobs = "secreto-de-jobs-para-los-tests";

    /// <summary>Abre sesión y devuelve los tokens.</summary>
    public async Task<SesionDto> LoginAsync(string email, string? password = null)
    {
        using var cliente = CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(email, password ?? Password));

        respuesta.EnsureSuccessStatusCode();

        return await respuesta.Content.ReadFromJsonAsync<SesionDto>()
               ?? throw new InvalidOperationException("El login no devolvió una sesión.");
    }

    /// <summary>Cliente con el <c>Authorization: Bearer</c> ya puesto.</summary>
    public async Task<HttpClient> ClienteDeAsync(string email)
    {
        var sesion = await LoginAsync(email);
        var cliente = CreateClient();

        cliente.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", sesion.AccessToken);

        return cliente;
    }

    /// <summary>
    /// Corre algo contra la base con la escritura cross-tenant habilitada, para sembrar
    /// datos de un tenant puntual sin pasar por un request.
    /// </summary>
    /// <remarks>
    /// La analítica se puebla así y no por el endpoint público porque los reportes miran
    /// una ventana de días: los eventos tienen que poder tener fecha vieja, y el endpoint
    /// —con razón— siempre los guarda con la de ahora.
    /// </remarks>
    public void ConLaBase(Action<AppDbContext> accion)
    {
        ArgumentNullException.ThrowIfNull(accion);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var _ = db.PermitirEscrituraCrossTenant();

        accion(db);
        db.SaveChanges();
    }

    /// <summary>
    /// Una automotora nueva con su Owner, en el plan pedido y con el pago cubierto hasta
    /// <paramref name="pagaHasta"/> (por defecto, dentro de un mes).
    /// </summary>
    /// <returns>El id de la automotora y el email de su Owner, que entra con <see cref="Password"/>.</returns>
    public (int TenantId, string EmailOwner) AutomotoraConPlan(string slug, string codigoDePlan, DateOnly? pagaHasta = null)
    {
        var email = $"owner@{slug}.uy";
        var tenantId = 0;

        ConLaBase(db =>
        {
            var tenant = new Tenant { Slug = slug, Nombre = $"Automotora {slug}" };
            db.Tenants.Add(tenant);
            db.SaveChanges();

            var plan = db.Planes.Single(p => p.Codigo == codigoDePlan);
            var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

            db.Suscripciones.Add(new Suscripcion
            {
                TenantId = tenant.Id,
                PlanId = plan.Id,
                Inicio = hoy.AddMonths(-3),
                PagaHasta = pagaHasta ?? hoy.AddMonths(1),
            });

            using var scope = Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(NuevoUsuario(email, $"Owner {slug}", RolUsuario.Owner, tenant.Id, hasher.Hash(Password)));

            tenantId = tenant.Id;
        });

        return (tenantId, email);
    }

    /// <summary>Un evento ya ocurrido, con su fecha.</summary>
    public static Evento Evento(int tenantId, int? vehiculoId, TipoEvento tipo, DateTime cuando, string? sesion = null)
        => new()
        {
            TenantId = tenantId,
            VehiculoId = vehiculoId,
            Tipo = tipo,
            SessionId = sesion,
            CreatedAt = cuando,
        };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Ni Development ni Production: sin Development no corre el seed de arranque, que
        // acá lo hace la fábrica.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<DbContextOptions<AppDbContext>>();
            servicios.RemoveAll<DbContextOptions>();
            servicios.RemoveAll<AppDbContext>();

            servicios.AddDbContext<AppDbContext>(opciones => opciones
                .UseSqlite(_conexion)
                .UseSnakeCaseNamingConvention());

            servicios.RemoveAll<IImageStorage>();
            servicios.AddSingleton<IImageStorage>(Almacenamiento);

            servicios.RemoveAll<IResolvedorDeDns>();
            servicios.AddSingleton<IResolvedorDeDns>(Dns);

            servicios.RemoveAll<INotificadorPorCorreo>();
            servicios.AddSingleton<INotificadorPorCorreo>(Correo);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _conexion.Dispose();
        }
    }

    private void Sembrar(AppDbContext db, IPasswordHasher hasher)
    {
        // Datos de dos tenants y un SuperAdmin sin tenant, sin ningún tenant resuelto: es
        // exactamente lo que la política de escritura bloquea, así que se declara.
        using var _ = db.PermitirEscrituraCrossTenant();

        var norte = new Tenant
        {
            Slug = "norte",
            Nombre = "Automotora Norte",
            DominioCustom = DominioDeNorte,

            // Verificado: es la automotora que ya tiene su dominio funcionando, y por eso
            // el sitio le responde por ahí.
            DominioVerificadoEn = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            ColorPrimario = "#059669",
            Whatsapp = "+59899111222",
        };

        // Sur declaró un dominio y todavía no lo verificó. Es el estado en el que está una
        // automotora entre que se lo cargan y que toca su DNS, y el sitio no le responde
        // por ahí hasta entonces.
        var sur = new Tenant
        {
            Slug = "sur",
            Nombre = "Automotora Sur",
            DominioCustom = DominioSinVerificarDeSur,
        };
        var apagada = new Tenant { Slug = "apagada", Nombre = "Automotora Apagada", Activo = false };

        db.Tenants.AddRange(norte, sur, apagada);
        db.SaveChanges();

        TenantNorte = norte.Id;
        TenantSur = sur.Id;

        // Plan Full y al día: los tests de siempre prueban el producto, no la cobranza. Los
        // de planes y vencimientos arman sus propias automotoras con AutomotoraConPlan.
        var full = db.Planes.Single(p => p.Codigo == CodigosDePlan.Full);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var tenant in new[] { norte, sur, apagada })
        {
            db.Suscripciones.Add(Suscripciones.IniciarConBonificacion(tenant.Id, full, hoy));
        }

        db.SaveChanges();

        var hash = hasher.Hash(Password);

        var ownerNorte = NuevoUsuario(EmailOwnerNorte, "Owner Norte", RolUsuario.Owner, norte.Id, hash);
        var vendedorNorte = NuevoUsuario(EmailVendedorNorte, "Vendedor Norte", RolUsuario.Seller, norte.Id, hash);
        var inactivoNorte = NuevoUsuario(EmailInactivoNorte, "Baja Norte", RolUsuario.Seller, norte.Id, hash);
        inactivoNorte.Activo = false;

        var ownerSur = NuevoUsuario(EmailOwnerSur, "Owner Sur", RolUsuario.Owner, sur.Id, hash);
        var superAdmin = NuevoUsuario(EmailSuperAdmin, "Super", RolUsuario.SuperAdmin, null, hash);

        db.Users.AddRange(ownerNorte, vendedorNorte, inactivoNorte, ownerSur, superAdmin);
        db.SaveChanges();

        OwnerDeNorte = ownerNorte.Id;
        VendedorDeNorte = vendedorNorte.Id;
        InactivoDeNorte = inactivoNorte.Id;
        OwnerDeSur = ownerSur.Id;

        SembrarCatalogoYStock(db, norte.Id, sur.Id);
    }

    private void SembrarCatalogoYStock(AppDbContext db, int norteId, int surId)
    {
        var marca = new Marca { Nombre = "Volkswagen" };
        db.Marcas.Add(marca);
        db.SaveChanges();

        var modelo = new Modelo { MarcaId = marca.Id, Nombre = "Gol", Carroceria = Carroceria.Hatchback };
        var deBaja = new Modelo
        {
            MarcaId = marca.Id,
            Nombre = "Fox",
            Carroceria = Carroceria.Hatchback,
            Activo = false,
        };

        db.Modelos.AddRange(modelo, deBaja);
        db.SaveChanges();

        MarcaId = marca.Id;
        ModeloId = modelo.Id;
        ModeloDadoDeBaja = deBaja.Id;

        var disponible = NuevoVehiculo(norteId, modelo.Id, 2019, 15_000m, EstadoVehiculo.Disponible);
        var vendido = NuevoVehiculo(norteId, modelo.Id, 2016, 9_500m, EstadoVehiculo.Vendido);
        var deSur = NuevoVehiculo(surId, modelo.Id, 2021, 22_000m, EstadoVehiculo.Disponible);
        var olvidado = NuevoVehiculo(norteId, modelo.Id, 2013, 6_500m, EstadoVehiculo.Disponible);

        // Relativa al reloj y no una fecha fija: lo que este vehículo representa es
        // "hace meses que está", y una constante deja de significar eso con el tiempo.
        olvidado.FechaPublicacion = DateTime.UtcNow.AddDays(-150);

        vendido.FechaVenta = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        vendido.PrecioVenta = 9_000m;

        db.Vehiculos.AddRange(disponible, vendido, deSur, olvidado);
        db.SaveChanges();

        VehiculoDeNorte = disponible.Id;
        VendidoDeNorte = vendido.Id;
        VehiculoDeSur = deSur.Id;
        OlvidadoDeNorte = olvidado.Id;

        db.VehiculoFotos.Add(new VehiculoFoto
        {
            VehiculoId = disponible.Id,
            Url = "https://cdn.ejemplo.com/tenants/1/vehiculos/1/portada.jpg",
            Orden = 0,
            EsPortada = true,
        });

        db.SaveChanges();
    }

    private static Vehiculo NuevoVehiculo(int tenantId, int modeloId, int anio, decimal precio, EstadoVehiculo estado)
        => new()
        {
            TenantId = tenantId,
            ModeloId = modeloId,
            Anio = anio,
            Kilometraje = 60_000,
            Combustible = Combustible.Nafta,
            Transmision = Transmision.Manual,
            Precio = precio,
            Moneda = Moneda.Usd,
            Estado = estado,
            PrecioCosto = precio - 2_000m,
            FechaPublicacion = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    private static User NuevoUsuario(string email, string nombre, RolUsuario rol, int? tenantId, string hash)
        => new()
        {
            TenantId = tenantId,
            Email = email,
            Nombre = nombre,
            Rol = rol,
            PasswordHash = hash,
        };
}
