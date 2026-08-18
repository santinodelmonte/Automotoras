using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Dominios;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Storage;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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

    public int VehiculoDeSur { get; private set; }

    /// <summary>Storage en memoria. Los tests no tocan el disco ni salen a la red.</summary>
    public AlmacenamientoDePrueba Almacenamiento { get; } = new();

    /// <summary>DNS en memoria, para poder publicar y borrar TXT desde un test.</summary>
    public DnsDePrueba Dns { get; } = new();

    public const string EmailOwnerNorte = "owner@norte.uy";
    public const string EmailOwnerSur = "owner@sur.uy";
    public const string EmailVendedorNorte = "vendedor@norte.uy";
    public const string EmailInactivoNorte = "baja@norte.uy";
    public const string EmailSuperAdmin = "super@saas.uy";

    public const string DominioDeNorte = "automotoranorte.uy";

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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Ni Development ni Production: sin Development no corre el seed de arranque, que
        // acá lo hace la fábrica.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuracion) => configuracion.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                // Presente pero sin usar: el DbContext se reemplaza más abajo por SQLite.
                ["ConnectionStrings:Default"] = "Server=no-se-usa;Database=no-se-usa;User Id=no;Password=no;",
                ["Jwt:Issuer"] = "automotora-saas-tests",
                ["Jwt:Audience"] = "automotora-saas-tests",
                ["Jwt:Secret"] = "secreto-de-tests-largo-y-aburrido-de-sobra-32",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["Jobs:Secret"] = SecretoDeJobs,
                ["Analytics:IpHashSalt"] = "sal-de-tests-estable",
            }));

        // El SQL de EF por consola tapa el resultado de la corrida: una suite con dos
        // fallas deja miles de líneas de SELECT entre el error y el final del log, y en CI
        // eso es la diferencia entre ver qué se rompió y no verlo.
        builder.ConfigureLogging(logging =>
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));

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

            servicios.RemoveAll<IConsultaDns>();
            servicios.AddSingleton<IConsultaDns>(Dns);
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
            ColorPrimario = "#059669",
            Whatsapp = "+59899111222",
        };

        var sur = new Tenant { Slug = "sur", Nombre = "Automotora Sur" };
        var apagada = new Tenant { Slug = "apagada", Nombre = "Automotora Apagada", Activo = false };

        db.Tenants.AddRange(norte, sur, apagada);
        db.SaveChanges();

        TenantNorte = norte.Id;
        TenantSur = sur.Id;

        // Verificado y principal: el sitio público de Norte se prueba entrando por su
        // dominio, y un dominio sin verificar no resuelve.
        db.Dominios.Add(new DominioDeTenant
        {
            TenantId = norte.Id,
            Dominio = DominioDeNorte,
            Estado = EstadoDeDominio.Verificado,
            TokenDeVerificacion = "token-de-norte",
            EsPrincipal = true,
            VerificadoEn = DateTime.UtcNow,
            UltimaVerificacion = DateTime.UtcNow,
        });

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

        vendido.FechaVenta = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        vendido.PrecioVenta = 9_000m;

        db.Vehiculos.AddRange(disponible, vendido, deSur);
        db.SaveChanges();

        VehiculoDeNorte = disponible.Id;
        VendidoDeNorte = vendido.Id;
        VehiculoDeSur = deSur.Id;

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
