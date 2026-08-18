using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Qué se está pidiendo en el mercado por un modelo y un año, en una fecha.
/// </summary>
/// <remarks>
/// Es global y no por tenant, igual que las cotizaciones y el catálogo: el precio de
/// mercado de un Corolla 2018 es el mismo para todas las automotoras, y guardarlo por
/// tenant sería multiplicar por cliente un dato que es uno solo.
/// <para>
/// Se guarda un snapshot por día y no un solo valor que se pisa, porque la pregunta que
/// importa no es "cuánto vale hoy" sino "cómo se movió". Un precio de referencia sin
/// historia no permite ver que el mercado bajó y que la unidad que lleva tres meses en
/// góndola ya está cara sin que nadie le haya tocado el precio.
/// </para>
/// </remarks>
public class PrecioDeMercado : ICreatedAt
{
    public int Id { get; set; }

    public int ModeloId { get; set; }
    public Modelo? Modelo { get; set; }

    public int Anio { get; set; }

    /// <summary>Moneda de los tres importes. En Uruguay, casi siempre dólares.</summary>
    public Moneda Moneda { get; set; }

    /// <summary>
    /// La mediana de lo publicado. Mediana y no promedio: en una muestra de avisos
    /// siempre hay dos o tres con un precio de fantasía, y el promedio los sigue.
    /// </summary>
    public decimal PrecioMediano { get; set; }

    public decimal PrecioMinimo { get; set; }
    public decimal PrecioMaximo { get; set; }

    /// <summary>
    /// Cuántas publicaciones respaldan el número.
    /// </summary>
    /// <remarks>
    /// Viaja con el dato y no se descarta, porque una mediana de tres avisos y una de
    /// doscientos no se leen igual. Quien muestra el precio decide con cuántas se anima;
    /// lo que no puede es no saberlo.
    /// </remarks>
    public int Publicaciones { get; set; }

    /// <summary>De dónde salió. Hoy siempre MercadoLibre; mañana puede haber otra.</summary>
    public required string Fuente { get; set; }

    /// <summary>Día del snapshot, sin hora.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
