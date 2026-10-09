using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;
using Microsoft.EntityFrameworkCore;

namespace AutomotoraSaaS.Tests.Persistence;

/// <summary>
/// Planes y suscripciones en la base: el catálogo sembrado y la regla de que un tenant
/// tiene como mucho una suscripción vigente.
/// </summary>
/// <remarks>
/// Cada test arma su propia base y no comparte la del fixture: escriben suscripciones, y
/// una vigente que deja un test es exactamente lo que hace fallar al siguiente.
/// </remarks>
public sealed class SuscripcionesTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 10, 8);

    private readonly BaseDeDatosDePrueba _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Los_tres_planes_de_la_propuesta_vienen_sembrados()
    {
        using var contexto = _db.CrearContexto(tenantId: null);

        var planes = contexto.Planes.OrderBy(p => p.Id).ToList();

        Assert.Collection(
            planes,
            p =>
            {
                Assert.Equal(CodigosDePlan.Vidriera, p.Codigo);
                Assert.Equal(4_000m, p.PrecioMensual);
                Assert.Equal(40, p.MaxVehiculos);
                Assert.Equal(2, p.MaxUsuarios);
                Assert.False(p.IncluyeReportes);
                Assert.False(p.IncluyeBenchmark);
                Assert.False(p.IncluyeDominioPropio);
            },
            p =>
            {
                Assert.Equal(CodigosDePlan.Demanda, p.Codigo);
                Assert.Equal(6_900m, p.PrecioMensual);
                Assert.Equal(120, p.MaxVehiculos);
                Assert.Equal(6, p.MaxUsuarios);
                Assert.True(p.IncluyeReportes);
                Assert.False(p.IncluyeBenchmark);
                Assert.True(p.IncluyeDominioPropio);
            },
            p =>
            {
                Assert.Equal(CodigosDePlan.Full, p.Codigo);
                Assert.Equal(9_900m, p.PrecioMensual);
                Assert.Null(p.MaxVehiculos);
                Assert.Null(p.MaxUsuarios);
                Assert.True(p.IncluyeReportes);
                Assert.True(p.IncluyeBenchmark);
                Assert.True(p.IncluyeDominioPropio);
            });

        Assert.All(planes, p => Assert.Equal(Moneda.Uyu, p.Moneda));
    }

    [Fact]
    public void Un_tenant_no_puede_tener_dos_suscripciones_vigentes()
    {
        Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Vidriera);

        Assert.Throws<DbUpdateException>(() => Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Full));
    }

    /// <summary>
    /// El cambio de plan: se cierra la vigente y se abre otra. Las cerradas no cuentan, y
    /// un tenant puede acumular todas las que haga falta en su historial.
    /// </summary>
    [Fact]
    public void Cerrar_la_vigente_permite_abrir_otra()
    {
        Cerrar(Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Vidriera));
        Cerrar(Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Demanda));
        Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Full);

        using var contexto = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        Assert.Equal(3, contexto.Suscripciones.Count());

        var vigente = contexto.Suscripciones.Include(s => s.Plan).Single(s => s.Fin == null);
        Assert.Equal(CodigosDePlan.Full, vigente.Plan!.Codigo);
    }

    [Fact]
    public void Dos_tenants_distintos_tienen_cada_uno_su_vigente()
    {
        Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Demanda);
        Suscribir(BaseDeDatosDePrueba.TenantB, CodigosDePlan.Demanda);

        using var contexto = _db.CrearContexto(tenantId: null);

        Assert.Equal(2, contexto.Suscripciones.IgnoreQueryFilters().Count(s => s.Fin == null));
    }

    [Fact]
    public void Un_tenant_no_ve_la_suscripcion_ni_los_pagos_de_otro()
    {
        Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Full);

        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        Assert.Empty(contextoB.Suscripciones.ToList());
        Assert.Empty(contextoB.Pagos.ToList());
    }

    /// <summary>
    /// Las suscripciones las escribe el SuperAdmin. Desde el request de un tenant no se
    /// puede dar de alta una suscripción para otro.
    /// </summary>
    [Fact]
    public void Un_tenant_no_puede_escribir_la_suscripcion_de_otro()
    {
        using var contextoB = _db.CrearContexto(BaseDeDatosDePrueba.TenantB);

        var full = contextoB.Planes.Single(p => p.Codigo == CodigosDePlan.Full);
        contextoB.Suscripciones.Add(
            Suscripciones.IniciarConBonificacion(BaseDeDatosDePrueba.TenantA, full, Hoy));

        Assert.Throws<TenantIsolationException>(() => contextoB.SaveChanges());
    }

    [Fact]
    public void La_suscripcion_inicial_queda_explicada_por_su_pago_bonificado()
    {
        var suscripcion = Suscribir(BaseDeDatosDePrueba.TenantA, CodigosDePlan.Demanda);

        using var contexto = _db.CrearContexto(BaseDeDatosDePrueba.TenantA);

        var pago = Assert.Single(contexto.Pagos.Where(p => p.SuscripcionId == suscripcion.Id).ToList());

        Assert.Equal(0m, pago.Monto);
        Assert.Equal(suscripcion.Inicio, pago.PeriodoDesde);
        Assert.Equal(suscripcion.PagaHasta, pago.PeriodoHasta);
    }

    private Suscripcion Suscribir(int tenantId, string codigoDePlan)
    {
        using var contexto = _db.CrearContexto(tenantId: null);
        using var _ = contexto.PermitirEscrituraCrossTenant();

        var plan = contexto.Planes.Single(p => p.Codigo == codigoDePlan);
        var suscripcion = Suscripciones.IniciarConBonificacion(tenantId, plan, Hoy);

        contexto.Suscripciones.Add(suscripcion);
        contexto.SaveChanges();

        return suscripcion;
    }

    private void Cerrar(Suscripcion suscripcion)
    {
        using var contexto = _db.CrearContexto(tenantId: null);
        using var _ = contexto.PermitirEscrituraCrossTenant();

        var guardada = contexto.Suscripciones.IgnoreQueryFilters().Single(s => s.Id == suscripcion.Id);
        guardada.Fin = Hoy;
        guardada.MotivoDeBaja = "Cambio de plan";

        contexto.SaveChanges();
    }
}
