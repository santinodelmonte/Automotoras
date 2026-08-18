using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
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

    public const string EmailOwnerNorte = "owner@norte.uy";
    public const string EmailOwnerSur = "owner@sur.uy";
    public const string EmailVendedorNorte = "vendedor@norte.uy";
    public const string EmailInactivoNorte = "baja@norte.uy";
    public const string EmailSuperAdmin = "super@saas.uy";

    public const string DominioDeNorte = "automotoranorte.uy";

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
            }));

        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<DbContextOptions<AppDbContext>>();
            servicios.RemoveAll<DbContextOptions>();
            servicios.RemoveAll<AppDbContext>();

            servicios.AddDbContext<AppDbContext>(opciones => opciones
                .UseSqlite(_conexion)
                .UseSnakeCaseNamingConvention());
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
            ColorPrimario = "#059669",
            Whatsapp = "+59899111222",
        };

        var sur = new Tenant { Slug = "sur", Nombre = "Automotora Sur" };
        var apagada = new Tenant { Slug = "apagada", Nombre = "Automotora Apagada", Activo = false };

        db.Tenants.AddRange(norte, sur, apagada);
        db.SaveChanges();

        TenantNorte = norte.Id;
        TenantSur = sur.Id;

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
    }

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
