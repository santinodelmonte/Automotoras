using AutomotoraSaaS.Core.Planes;

namespace AutomotoraSaaS.Tests.Planes;

/// <summary>
/// Los cinco estados del ciclo de cobro, con las fechas límite exactas. Con 7 días de aviso
/// y 10 de gracia, que son los de la configuración por defecto.
/// </summary>
public sealed class CicloDeCobroTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 8);
    private static readonly OpcionesDeCobranza Opciones = new();

    [Theory]
    [InlineData(30, EstadoDeCobro.Vigente)]
    [InlineData(8, EstadoDeCobro.Vigente)]
    [InlineData(7, EstadoDeCobro.PorVencer)]
    [InlineData(1, EstadoDeCobro.PorVencer)]
    [InlineData(0, EstadoDeCobro.PorVencer)] // vence hoy: hoy todavía está pago
    [InlineData(-1, EstadoDeCobro.Gracia)]
    [InlineData(-10, EstadoDeCobro.Gracia)]
    [InlineData(-11, EstadoDeCobro.Suspendido)]
    [InlineData(-400, EstadoDeCobro.Suspendido)]
    public void El_estado_sale_de_los_dias_que_faltan_para_vencer(int dias, EstadoDeCobro esperado)
    {
        Assert.Equal(esperado, CicloDeCobro.Evaluar(Hoy.AddDays(dias), Hoy, Opciones));
    }

    [Fact]
    public void Sin_suscripcion_vigente_esta_suspendido()
    {
        Assert.Equal(EstadoDeCobro.Suspendido, CicloDeCobro.Evaluar(null, Hoy, Opciones));
    }

    [Fact]
    public void Los_umbrales_salen_de_la_configuracion()
    {
        var estrictas = new OpcionesDeCobranza { DiasDeAviso = 3, DiasDeGracia = 0 };

        Assert.Equal(EstadoDeCobro.Vigente, CicloDeCobro.Evaluar(Hoy.AddDays(4), Hoy, estrictas));
        Assert.Equal(EstadoDeCobro.PorVencer, CicloDeCobro.Evaluar(Hoy.AddDays(3), Hoy, estrictas));
        Assert.Equal(EstadoDeCobro.Suspendido, CicloDeCobro.Evaluar(Hoy.AddDays(-1), Hoy, estrictas));
    }

    [Theory]
    [InlineData(EstadoDeCobro.Vigente, true)]
    [InlineData(EstadoDeCobro.PorVencer, true)]
    [InlineData(EstadoDeCobro.Gracia, true)] // atrasarse tres días no apaga la vidriera
    [InlineData(EstadoDeCobro.Suspendido, false)]
    public void El_sitio_solo_se_apaga_en_suspension(EstadoDeCobro estado, bool publicado)
    {
        Assert.Equal(publicado, CicloDeCobro.SitioPublicado(estado));
    }
}
