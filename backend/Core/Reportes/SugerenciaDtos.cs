namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Qué hacer con una demanda que quedó sin atender.
/// </summary>
/// <remarks>
/// La distinción es la que cambia la decisión. "Nadie encontró una pickup" y "tenés tres
/// pickups y ninguna sirvió" mandan a lugares opuestos: una es salir a comprar, la otra
/// es revisar precio, año o fotos de lo que ya está en el patio. Sugerir comprar cuando
/// el stock ya existe es la forma más cara de equivocarse.
/// </remarks>
public enum TipoDeSugerencia
{
    /// <summary>No hay una sola unidad publicada de eso. Es demanda que se va entera.</summary>
    Comprar = 1,

    /// <summary>Hay unidades y ninguna entró en lo que se pedía. El stock está, el encaje no.</summary>
    RevisarLoQueTenes = 2,
}

/// <summary>
/// Umbrales de las sugerencias de compra.
/// </summary>
public static class UmbralesDeSugerencia
{
    /// <summary>
    /// Visitas distintas por debajo de las cuales no se sugiere nada.
    /// </summary>
    /// <remarks>
    /// Una sola persona buscando algo raro no es una señal de mercado, y una sugerencia
    /// por cada curioso convierte la pantalla en ruido. Con ruido, la primera sugerencia
    /// buena se pierde entre veinte que no lo son y el dueño deja de mirar la pantalla —y
    /// ahí el reporte no vale nada, por más que las cuentas estén bien.
    /// </remarks>
    public const int VisitasMinimas = 3;

    /// <summary>Cuántas sugerencias se devuelven como mucho.</summary>
    public const int Maximo = 20;

    /// <summary>Ventana por defecto: noventa días, no treinta.</summary>
    /// <remarks>
    /// Comprar una unidad es una decisión de miles de dólares y lleva semanas concretarla.
    /// Un mes de búsquedas alcanza para ver una tendencia de tráfico, no para justificar
    /// una compra.
    /// </remarks>
    public const int DiasPorDefecto = 90;
}

/// <summary>
/// Una sugerencia de compra, con la evidencia que la sostiene al lado.
/// </summary>
/// <remarks>
/// Va estructurada y sin la frase ya escrita. La frase la arma el panel, que es donde se
/// sabe en qué idioma y con cuánto lugar se va a mostrar; lo que no puede armar el panel
/// —qué se pidió, cuántos lo pidieron y qué hay en el patio— es exactamente lo que viaja
/// acá.
/// </remarks>
/// <param name="Visitas">
/// Visitas distintas que buscaron esto y no encontraron nada. Es la cifra que ordena la
/// lista: mide cuántos compradores se fueron, no cuántas veces se apretó buscar.
/// </param>
/// <param name="UnidadesEnStock">
/// Unidades publicadas que caen en el mismo modelo o carrocería. Cero es lo que convierte
/// la sugerencia en una compra.
/// </param>
/// <param name="PresupuestoTipico">
/// La mediana de los topes de precio pedidos, en la moneda más pedida del grupo. La
/// mediana y no el promedio: un solo visitante con un tope de cien mil correría el
/// promedio hasta un presupuesto que no tiene nadie.
/// </param>
public sealed record SugerenciaDeCompraDto(
    string Tipo,
    int? MarcaId,
    string? Marca,
    int? ModeloId,
    string? Modelo,
    string? Carroceria,
    int? AnioDesde,
    int? AnioHasta,
    string? Moneda,
    decimal? PresupuestoTipico,
    int Visitas,
    int Busquedas,
    DateTime UltimaVez,
    int UnidadesEnStock);
