using System.Globalization;
using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Contexto único de la aplicación. Una sola base para todos los tenants: los datos
/// agregados son el activo del producto y separarlos por cliente destruiría la propuesta
/// de valor.
/// </summary>
/// <remarks>
/// El aislamiento entre tenants se sostiene sobre dos mecanismos, no uno:
/// <list type="number">
///   <item><b>Lectura:</b> filtros globales por tenant en toda entidad de tenant. Olvidarse
///   un <c>WHERE</c> no alcanza para filtrar datos ajenos.</item>
///   <item><b>Escritura:</b> los filtros globales no protegen los <c>INSERT</c> ni los
///   <c>UPDATE</c>. Por eso <see cref="SaveChanges(bool)"/> sella el tenant en las altas y
///   rechaza cualquier escritura sobre datos de otro tenant.</item>
/// </list>
/// Cuando no hay tenant resuelto, las lecturas devuelven cero filas y las escrituras
/// lanzan. Ante la duda, nada: fallar cerrado.
/// </remarks>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Centinela para "ningún tenant". Los ids son autoincrementales y arrancan en 1, así
    /// que nunca coincide con un tenant real y los filtros devuelven cero filas.
    /// </summary>
    private const int SinTenant = 0;

    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private bool _permitirEscrituraCrossTenant;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
        : base(options)
    {
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Tenant del request, o <see cref="SinTenant"/>. Los filtros globales leen de acá.
    /// EF la reevalúa en cada consulta, así que dos contextos con tenants distintos ven
    /// datos distintos aunque compartan el modelo cacheado.
    /// </summary>
    private int CurrentTenantId => _tenantContext.TenantId ?? SinTenant;

    // Catálogo global, sin tenant.
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<DominioDeTenant> Dominios => Set<DominioDeTenant>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Modelo> Modelos => Set<Modelo>();
    public DbSet<VersionVehiculo> Versiones => Set<VersionVehiculo>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<PrecioReferencia> PreciosReferencia => Set<PrecioReferencia>();

    // Identidad. Los usuarios llevan tenant anulable: el SuperAdmin es cross-tenant.
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Datos de tenant.
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<VehiculoFoto> VehiculoFotos => Set<VehiculoFoto>();
    public DbSet<Evento> Eventos => Set<Evento>();
    public DbSet<Busqueda> Busquedas => Set<Busqueda>();
    public DbSet<SolicitudModelo> SolicitudesModelo => Set<SolicitudModelo>();

    /// <summary>
    /// Habilita escrituras cross-tenant mientras dure el <see cref="IDisposable"/>.
    /// </summary>
    /// <remarks>
    /// Solo para los endpoints de SuperAdmin bajo <c>/api/admin/*</c> y para el seed de
    /// desarrollo. Nunca desde un endpoint de tenant: es una decisión consciente y
    /// auditable, no un flag opcional que se pueda colar en un request normal.
    /// </remarks>
    public IDisposable PermitirEscrituraCrossTenant()
    {
        _permitirEscrituraCrossTenant = true;
        return new AlcanceCrossTenant(this);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // VehiculoFoto tiene un filtro global que navega a Vehiculo, que a su vez es una
        // relación requerida y también está filtrado. EF avisa de esa combinación porque
        // suele ser un accidente; acá es exactamente lo buscado: una foto cuyo vehículo
        // pertenece a otro tenant tiene que ser invisible, no aparecer con el vehículo en
        // null.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtros globales por tenant.
        //
        // Van escritos uno por uno a propósito, en vez de aplicarse por reflexión: es la
        // frontera de seguridad del producto y tiene que poder auditarse leyendo. La red
        // de contención contra olvidos es el test que recorre todas las entidades que
        // implementan ITenantEntity y falla si alguna quedó sin filtro.
        modelBuilder.Entity<Vehiculo>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Evento>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Busqueda>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SolicitudModelo>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        // Las fotos no tienen tenant_id propio: pertenecen al tenant de su vehículo.
        modelBuilder.Entity<VehiculoFoto>()
            .HasQueryFilter(f => f.Vehiculo!.TenantId == CurrentTenantId);

        // Los usuarios llevan tenant anulable y por eso no implementan ITenantEntity,
        // pero se filtran con la misma severidad. Un SuperAdmin (tenant nulo) no aparece
        // en las consultas de un tenant: se administra por /api/admin/*.
        modelBuilder.Entity<User>().HasQueryFilter(u => u.TenantId == CurrentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarPoliticas();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        AplicarPoliticas();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AplicarPoliticas()
    {
        SellarTimestamps();
        VerificarPropiedadDelTenant();
    }

    private void SellarTimestamps()
    {
        var ahora = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ICreatedAt creado && creado.CreatedAt == default)
                    {
                        creado.CreatedAt = ahora;
                    }

                    if (entry.Entity is IUpdatedAt nuevo)
                    {
                        nuevo.UpdatedAt = ahora;
                    }

                    break;

                case EntityState.Modified:
                    if (entry.Entity is IUpdatedAt modificado)
                    {
                        modificado.UpdatedAt = ahora;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Sella el tenant en las altas y rechaza cualquier escritura sobre datos de otro
    /// tenant. Es lo que cubre el agujero que los filtros globales dejan abierto: un
    /// <c>INSERT</c> con un <c>tenant_id</c> ajeno no lo detiene ningún <c>WHERE</c>.
    /// </summary>
    private void VerificarPropiedadDelTenant()
    {
        if (_permitirEscrituraCrossTenant)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var tenantDelRequest = TenantRequerido(entry.Entity.GetType().Name);

            if (entry.State == EntityState.Added && entry.Entity.TenantId == SinTenant)
            {
                entry.Entity.TenantId = tenantDelRequest;
                continue;
            }

            if (entry.Entity.TenantId != tenantDelRequest)
            {
                throw new TenantIsolationException(
                    $"Se intentó {DescribirOperacion(entry.State)} un {entry.Entity.GetType().Name} " +
                    $"del tenant {entry.Entity.TenantId} desde el tenant {tenantDelRequest}.");
            }
        }

        VerificarPropiedadDeLosUsuarios();
    }

    /// <summary>
    /// Lo mismo que <see cref="VerificarPropiedadDelTenant"/>, pero para los usuarios, que
    /// quedan afuera de <c>ITenantEntity</c> porque su tenant es anulable.
    /// </summary>
    /// <remarks>
    /// Sin esto, la gestión de vendedores sería el único lugar del sistema donde un
    /// <c>tenant_id</c> ajeno pasa sin que nadie lo mire — justo el agujero por el que un
    /// Owner podría darse de alta un usuario dentro de otra automotora. Crear un
    /// SuperAdmin (tenant nulo) requiere el escape cross-tenant explícito.
    /// </remarks>
    private void VerificarPropiedadDeLosUsuarios()
    {
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var tenantDelRequest = TenantRequerido(nameof(User));

            if (entry.State == EntityState.Added && entry.Entity.TenantId is null)
            {
                entry.Entity.TenantId = tenantDelRequest;
                continue;
            }

            if (entry.Entity.TenantId != tenantDelRequest)
            {
                throw new TenantIsolationException(
                    $"Se intentó {DescribirOperacion(entry.State)} un {nameof(User)} " +
                    $"del tenant {entry.Entity.TenantId?.ToString(CultureInfo.InvariantCulture) ?? "global"} " +
                    $"desde el tenant {tenantDelRequest}.");
            }
        }
    }

    private int TenantRequerido(string nombreEntidad)
        => _tenantContext.TenantId
           ?? throw new TenantIsolationException(
               $"Se intentó escribir un {nombreEntidad} sin ningún tenant resuelto. " +
               "Para operaciones cross-tenant usá PermitirEscrituraCrossTenant() de forma explícita.");

    private static string DescribirOperacion(EntityState estado) => estado switch
    {
        EntityState.Added => "insertar",
        EntityState.Modified => "modificar",
        EntityState.Deleted => "borrar",
        _ => "tocar",
    };

    private sealed class AlcanceCrossTenant : IDisposable
    {
        private readonly AppDbContext _context;

        public AlcanceCrossTenant(AppDbContext context) => _context = context;

        public void Dispose() => _context._permitirEscrituraCrossTenant = false;
    }
}
