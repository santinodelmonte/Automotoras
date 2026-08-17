using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Infrastructure.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// Aislamiento de escritura. Los filtros globales solo protegen las lecturas: un
/// <c>INSERT</c> con un <c>tenant_id</c> ajeno no lo detiene ningún <c>WHERE</c>. Estos
/// tests cubren el otro lado.
/// </summary>
public sealed class EscrituraDeTenantTests : IClassFixture<BaseDeDatosDePrueba>
{
    private readonly BaseDeDatosDePrueba _db;

    public EscrituraDeTenantTests(BaseDeDatosDePrueba db)
    {
        _db = db;
    }

    [Fact]
    public void Al_insertar_sin_tenant_se_sella_el_del_request()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var vehiculo = NuevoVehiculo();
        contextoA.Vehiculos.Add(vehiculo);
        contextoA.SaveChanges();

        Assert.Equal(BaseDeDatosDePrueba.TenantA, vehiculo.TenantId);
    }

    [Fact]
    public void Insertar_con_el_tenant_de_otro_lanza()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var vehiculo = NuevoVehiculo();
        vehiculo.TenantId = BaseDeDatosDePrueba.TenantB;
        contextoA.Vehiculos.Add(vehiculo);

        var ex = Assert.Throws<TenantIsolationException>(() => contextoA.SaveChanges());
        Assert.Contains("insertar", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El caso feo: cargar un registro ajeno salteando los filtros y editarlo igual.
    /// La lectura se puede forzar; la escritura no.
    /// </summary>
    [Fact]
    public void Modificar_un_registro_ajeno_leido_con_IgnoreQueryFilters_lanza()
    {
        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        var ajeno = contextoB.Vehiculos
            .IgnoreQueryFilters()
            .Single(v => v.Id == _db.VehiculoDeA);

        ajeno.Precio = 1m;

        var ex = Assert.Throws<TenantIsolationException>(() => contextoB.SaveChanges());
        Assert.Contains("modificar", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mover_un_registro_a_otro_tenant_lanza()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var propio = contextoA.Vehiculos.First();
        propio.TenantId = BaseDeDatosDePrueba.TenantB;

        Assert.Throws<TenantIsolationException>(() => contextoA.SaveChanges());
    }

    [Fact]
    public void Sin_tenant_resuelto_no_se_puede_escribir()
    {
        using var contexto = _db.CrearContexto(tenantId: null);

        contexto.Vehiculos.Add(NuevoVehiculo());

        var ex = Assert.Throws<TenantIsolationException>(() => contexto.SaveChanges());
        Assert.Contains("sin ningún tenant resuelto", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void El_escape_cross_tenant_habilita_la_escritura_y_se_cierra_al_salir()
    {
        using var contexto = _db.CrearContexto(tenantId: null);

        var vehiculo = NuevoVehiculo();
        vehiculo.TenantId = BaseDeDatosDePrueba.TenantB;

        using (var _ = contexto.PermitirEscrituraCrossTenant())
        {
            contexto.Vehiculos.Add(vehiculo);
            contexto.SaveChanges();
        }

        Assert.True(vehiculo.Id > 0);

        // Fuera del alcance vuelve a estar cerrado.
        contexto.Vehiculos.Add(NuevoVehiculo());
        Assert.Throws<TenantIsolationException>(() => contexto.SaveChanges());
    }

    [Fact]
    public void El_tenant_del_request_no_se_puede_cambiar_una_vez_resuelto()
    {
        var tenantContext = new TenantContext();
        tenantContext.Resolver(BaseDeDatosDePrueba.TenantA);

        Assert.Throws<TenantIsolationException>(
            () => tenantContext.Resolver(BaseDeDatosDePrueba.TenantB));
    }

    [Fact]
    public void Las_altas_quedan_selladas_con_created_at_y_updated_at()
    {
        using var contextoA = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var vehiculo = NuevoVehiculo();
        contextoA.Vehiculos.Add(vehiculo);
        contextoA.SaveChanges();

        Assert.NotEqual(default, vehiculo.CreatedAt);
        Assert.NotEqual(default, vehiculo.UpdatedAt);
    }

    private static Vehiculo NuevoVehiculo() => new()
    {
        ModeloId = 1,
        Anio = 2019,
        Kilometraje = 50_000,
        Combustible = Combustible.Diesel,
        Transmision = Transmision.Automatica,
        Precio = 18_000m,
        Moneda = Moneda.Usd,
        Estado = EstadoVehiculo.Disponible,
        FechaPublicacion = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
    };
}
