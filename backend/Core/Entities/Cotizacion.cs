namespace AutomotoraSaaS.Core.Entities;

/// <summary>
/// Cotización del dólar para una fecha. Una fila por día, poblada por un job externo.
/// </summary>
/// <remarks>
/// Es global, no por tenant. Sin cotizaciones históricas no se pueden comparar precios
/// publicados en monedas distintas a lo largo del tiempo, y sin eso los reportes de
/// demanda no significan nada.
/// </remarks>
public class Cotizacion
{
    public int Id { get; set; }

    /// <summary>Fecha de la cotización, sin hora. Única.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Pesos uruguayos por dólar.</summary>
    public decimal UsdUyu { get; set; }
}
