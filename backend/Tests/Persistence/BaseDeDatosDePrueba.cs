using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using AutomotoraSaaS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// Base SQLite en memoria con dos tenants cargados, para probar los filtros globales sin
/// depender de un MySQL levantado.
/// </summary>
/// <remarks>
/// SQLite y no el proveedor InMemory: el InMemory no traduce a SQL y evalúa los filtros
/// en memoria con semántica de C#, así que un filtro roto puede pasar el test igual. Acá
/// lo que se prueba es la consulta real, con la semántica de NULL de SQL.
/// <para>
/// Lo que esto no cubre es la migración contra MySQL. Eso necesita un MySQL de verdad.
/// </para>
/// </remarks>
public sealed class BaseDeDatosDePrueba : IDisposable
{
    public const int TenantA = 1;
    public const int TenantB = 2;

    private readonly SqliteConnection _conexion;
    private readonly DbContextOptions<AppDbContext> _opciones;

    public BaseDeDatosDePrueba()
    {
        _conexion = new SqliteConnection("Filename=:memory:");
        _conexion.Open();

        _opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexion)
            .UseSnakeCaseNamingConvention()
            .Options;

        using var contexto = CrearContexto(tenantId: null);
        contexto.Database.EnsureCreated();
        Sembrar(contexto);
    }

    /// <summary>Id del vehículo del tenant A. Se usa para pedirlo desde el tenant B.</summary>
    public int VehiculoDeA { get; private set; }

    /// <summary>Id del vehículo del tenant B.</summary>
    public int VehiculoDeB { get; private set; }

    /// <summary>Id de una foto del vehículo del tenant A.</summary>
    public int FotoDeA { get; private set; }

    /// <summary>
    /// Crea un contexto atado a un tenant. Con <c>null</c> queda sin tenant resuelto, que
    /// es como llega un request antes de pasar por la resolución.
    /// </summary>
    public AppDbContext CrearContexto(int? tenantId)
    {
        var tenantContext = new TenantContext();

        if (tenantId is not null)
        {
            tenantContext.Resolver(tenantId.Value);
        }

        return new AppDbContext(_opciones, tenantContext, TimeProvider.System);
    }

    public void Dispose() => _conexion.Dispose();

    private void Sembrar(AppDbContext contexto)
    {
        // El seed escribe datos de los dos tenants sin ningún tenant resuelto: es
        // exactamente el caso que la política de escritura bloquea, así que se declara
        // explícito.
        using var _ = contexto.PermitirEscrituraCrossTenant();

        contexto.Tenants.AddRange(
            new Tenant { Id = TenantA, Slug = "norte", Nombre = "Automotora Norte" },
            new Tenant { Id = TenantB, Slug = "sur", Nombre = "Automotora Sur" });

        var marca = new Marca { Id = 1, Nombre = "Volkswagen" };
        var modelo = new Modelo { Id = 1, MarcaId = 1, Nombre = "Gol", Carroceria = Carroceria.Hatchback };
        contexto.Marcas.Add(marca);
        contexto.Modelos.Add(modelo);

        contexto.Users.AddRange(
            new User
            {
                Id = 1,
                TenantId = TenantA,
                Email = "owner@norte.uy",
                PasswordHash = "x",
                Nombre = "Owner Norte",
                Rol = RolUsuario.Owner,
            },
            new User
            {
                Id = 2,
                TenantId = TenantB,
                Email = "owner@sur.uy",
                PasswordHash = "x",
                Nombre = "Owner Sur",
                Rol = RolUsuario.Owner,
            },
            new User
            {
                Id = 3,
                TenantId = null,
                Email = "super@saas.uy",
                PasswordHash = "x",
                Nombre = "Super",
                Rol = RolUsuario.SuperAdmin,
            });

        var vehiculoA = NuevoVehiculo(TenantA, 2018, 15_000m);
        var vehiculoB = NuevoVehiculo(TenantB, 2020, 22_000m);
        contexto.Vehiculos.AddRange(vehiculoA, vehiculoB);

        contexto.SaveChanges();

        VehiculoDeA = vehiculoA.Id;
        VehiculoDeB = vehiculoB.Id;

        var foto = new VehiculoFoto
        {
            VehiculoId = vehiculoA.Id,
            Url = "https://cdn.ejemplo.com/a.jpg",
            Orden = 0,
            EsPortada = true,
        };
        contexto.VehiculoFotos.Add(foto);

        contexto.Eventos.AddRange(
            NuevoEvento(TenantA, vehiculoA.Id),
            NuevoEvento(TenantB, vehiculoB.Id));

        contexto.SaveChanges();

        FotoDeA = foto.Id;
    }

    private static Vehiculo NuevoVehiculo(int tenantId, int anio, decimal precio) => new()
    {
        TenantId = tenantId,
        ModeloId = 1,
        Anio = anio,
        Kilometraje = 80_000,
        Combustible = Combustible.Nafta,
        Transmision = Transmision.Manual,
        Precio = precio,
        Moneda = Moneda.Usd,
        Estado = EstadoVehiculo.Disponible,
        FechaPublicacion = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static Evento NuevoEvento(int tenantId, int vehiculoId) => new()
    {
        TenantId = tenantId,
        VehiculoId = vehiculoId,
        Tipo = TipoEvento.ViewFicha,
        SessionId = "sesion",
    };
}
