using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Precio de mercado de un modelo y año, en una fecha, según una fuente externa.
/// </summary>
/// <remarks>
/// Es global, no por tenant: lo que vale un Gol 2018 en el mercado es lo mismo para todas
/// las automotoras. Y es exactamente la clase de dato que justifica la decisión de una
/// sola base — se releva una vez y lo aprovechan todos.
/// <para>
/// Un snapshot por día y por fuente, no un valor que se pisa. Sin la serie histórica no se
/// puede decir si un modelo se está desvalorizando, que es la mitad de para qué sirve
/// tener el dato.
/// </para>
/// </remarks>
public class PrecioReferencia : ICreatedAt
{
    public int Id { get; set; }

    public int ModeloId { get; set; }
    public Modelo? Modelo { get; set; }

    public int Anio { get; set; }

    /// <summary>Día del relevamiento, sin hora.</summary>
    public DateOnly Fecha { get; set; }

    public Moneda Moneda { get; set; }

    public decimal Promedio { get; set; }
    public decimal Minimo { get; set; }
    public decimal Maximo { get; set; }

    /// <summary>
    /// Cuántas publicaciones se promediaron. Un promedio de tres publicaciones y uno de
    /// doscientas no valen lo mismo, y quien lo lea tiene que poder distinguirlos.
    /// </summary>
    public int Muestras { get; set; }

    /// <summary>De dónde salió. Hoy <c>MercadoLibre</c>; mañana puede haber otra.</summary>
    public required string Fuente { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
