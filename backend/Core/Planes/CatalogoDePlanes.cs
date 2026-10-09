namespace AutomotoraSaaS.Core.Planes;

/// <summary>
/// Los códigos de los planes que vende la propuesta comercial.
/// </summary>
/// <remarks>
/// Los planes en sí —precio, topes, qué incluyen— están en la base. Acá solo están los
/// códigos, que son lo único que el código necesita nombrar.
/// </remarks>
public static class CodigosDePlan
{
    public const string Vidriera = "vidriera";
    public const string Demanda = "demanda";
    public const string Full = "full";

    /// <summary>
    /// El que se asigna a una automotora nueva si el alta no dice otro. Es el que la
    /// propuesta recomienda.
    /// </summary>
    public const string PorDefecto = Demanda;
}
