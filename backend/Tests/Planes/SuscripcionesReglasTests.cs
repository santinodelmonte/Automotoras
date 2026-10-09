using System.Globalization;
using AutomotoraSaaS.Core.Entities;
using AutomotoraSaaS.Core.Enums;
using AutomotoraSaaS.Core.Planes;

namespace AutomotoraSaaS.Tests.Planes;

public sealed class SuscripcionesReglasTests
{
    private static readonly Plan Demanda = new()
    {
        Id = 2,
        Codigo = CodigosDePlan.Demanda,
        Nombre = "Demanda",
        PrecioMensual = 6_900m,
        Moneda = Moneda.Uyu,
    };

    [Theory]
    [InlineData("2026-10-08", "2026-12-07")]
    [InlineData("2026-01-01", "2026-02-28")]
    [InlineData("2026-12-31", "2027-02-27")]
    public void Los_dos_meses_bonificados_cubren_hasta_el_dia_anterior(string inicio, string pagaHasta)
    {
        var suscripcion = Suscripciones.IniciarConBonificacion(
            tenantId: 7, Demanda, DateOnly.Parse(inicio, CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(pagaHasta, CultureInfo.InvariantCulture), suscripcion.PagaHasta);
    }

    [Fact]
    public void La_suscripcion_nace_vigente_y_con_un_pago_de_monto_cero_que_cubre_todo_el_periodo()
    {
        var inicio = new DateOnly(2026, 10, 8);

        var suscripcion = Suscripciones.IniciarConBonificacion(tenantId: 7, Demanda, inicio);

        Assert.Null(suscripcion.Fin);
        Assert.Equal(7, suscripcion.TenantId);
        Assert.Equal(Demanda.Id, suscripcion.PlanId);

        var pago = Assert.Single(suscripcion.Pagos);
        Assert.Equal(7, pago.TenantId);
        Assert.Equal(0m, pago.Monto);
        Assert.Equal(Moneda.Uyu, pago.Moneda);
        Assert.Equal(inicio, pago.PeriodoDesde);
        Assert.Equal(suscripcion.PagaHasta, pago.PeriodoHasta);
        Assert.Equal(Suscripciones.MedioBonificacion, pago.Medio);
        Assert.False(string.IsNullOrWhiteSpace(pago.Nota));
    }
}
