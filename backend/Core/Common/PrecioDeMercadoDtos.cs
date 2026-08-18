using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Un snapshot de precio de mercado, tal como lo manda el job.
/// </summary>
/// <remarks>
/// Igual que con las cotizaciones, los números los trae quien dispara el job: la API no
/// sale a consultar MercadoLibre. Acá el argumento pesa todavía más, porque un barrido de
/// precios no es una llamada saliente sino una por modelo y año — decenas o cientos. En
/// shared hosting IIS eso es tomarse los hilos del app pool que atiende a todos los
/// tenants para llenar una tabla que a nadie le urge al segundo.
/// <para>
/// El script que consulta MercadoLibre y llama a este endpoint está en
/// <c>tools/precios-de-mercado.mjs</c>.
/// </para>
/// </remarks>
/// <param name="Publicaciones">Cuántos avisos respaldan la mediana.</param>
public sealed record SnapshotDePrecioRequest(
    int ModeloId,
    int Anio,
    string Moneda,
    decimal PrecioMediano,
    decimal PrecioMinimo,
    decimal PrecioMaximo,
    int Publicaciones);

/// <summary>Cuerpo de <c>POST /api/jobs/precios-de-mercado</c>.</summary>
/// <param name="Fecha">Día del snapshot. Es el mismo para todo el lote.</param>
/// <param name="Fuente">De dónde salieron los precios.</param>
/// <param name="Precios">
/// El lote entero en un request. Un request por modelo serían cientos de conexiones para
/// escribir cientos de filas chicas, y el cron tendría que reintentar cada una por su
/// cuenta.
/// </param>
public sealed record RegistrarPreciosDeMercadoRequest(
    DateOnly Fecha,
    string Fuente,
    IReadOnlyList<SnapshotDePrecioRequest> Precios);

/// <summary>
/// Un modelo que hay que salir a cotizar, con los años que están publicados.
/// </summary>
/// <remarks>
/// Lleva los nombres de marca y modelo porque el que consulta MercadoLibre busca por
/// texto, no por id: el id del catálogo propio no significa nada del otro lado.
/// </remarks>
public sealed record ModeloACotizarDto(int ModeloId, string Marca, string Modelo, IReadOnlyList<int> Anios);

/// <summary>Resultado del job: cuántos snapshots se guardaron y cuántos se actualizaron.</summary>
public sealed record ResultadoDePreciosDto(int Guardados, int Actualizados);

/// <summary>
/// El precio de referencia de un modelo y año, con la fecha del snapshot al lado.
/// </summary>
/// <remarks>
/// La fecha viaja siempre. Un precio de referencia sin decir de cuándo es invita a
/// comparar contra un número de hace seis meses como si fuera de hoy.
/// </remarks>
public sealed record PrecioDeMercadoDto(
    decimal PrecioMediano,
    string Moneda,
    int Publicaciones,
    DateOnly Fecha,
    string Fuente);

public sealed class RegistrarPreciosDeMercadoRequestValidator
    : AbstractValidator<RegistrarPreciosDeMercadoRequest>
{
    /// <summary>
    /// Tope de snapshots por request. Con el catálogo entero por doce años de antigüedad
    /// no se llega ni cerca; lo que corta es que alguien mande un archivo entero por error.
    /// </summary>
    public const int MaximoPorLote = 2_000;

    public RegistrarPreciosDeMercadoRequestValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.Fuente).NotEmpty().MaximumLength(40);

        RuleFor(x => x.Fecha)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime).AddDays(1))
            .WithMessage("El snapshot no puede ser de una fecha futura.");

        RuleFor(x => x.Precios)
            .NotEmpty()
            .WithMessage("El lote no trae ningún precio.");

        RuleFor(x => x.Precios.Count)
            .LessThanOrEqualTo(MaximoPorLote)
            .WithMessage($"No se aceptan más de {MaximoPorLote} precios por request.");

        RuleForEach(x => x.Precios).SetValidator(new SnapshotDePrecioRequestValidator(reloj));
    }
}

public sealed class SnapshotDePrecioRequestValidator : AbstractValidator<SnapshotDePrecioRequest>
{
    /// <summary>
    /// Publicaciones mínimas para aceptar un snapshot.
    /// </summary>
    /// <remarks>
    /// Con dos avisos la mediana es el precio que puso una persona. No se guarda a medias
    /// ni se guarda marcado: se rechaza, porque un número guardado termina mostrado, y un
    /// precio de referencia equivocado es peor que no tener ninguno — el que no está se
    /// nota, el que está mal se cree.
    /// </remarks>
    public const int PublicacionesMinimas = 3;

    public SnapshotDePrecioRequestValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var anioMaximo = reloj.GetUtcNow().UtcDateTime.Year + 1;

        RuleFor(x => x.ModeloId).GreaterThan(0);

        RuleFor(x => x.Anio)
            .InclusiveBetween(1950, anioMaximo)
            .WithMessage("El año no parece real.");

        RuleFor(x => x.Moneda)
            .Must(Enumeraciones.EsValido<Moneda>)
            .WithMessage("La moneda no es válida.");

        RuleFor(x => x.Publicaciones)
            .GreaterThanOrEqualTo(PublicacionesMinimas)
            .WithMessage($"Hacen falta al menos {PublicacionesMinimas} publicaciones para un precio de referencia.");

        RuleFor(x => x.PrecioMediano).GreaterThan(0m);
        RuleFor(x => x.PrecioMinimo).GreaterThan(0m);

        RuleFor(x => x.PrecioMaximo)
            .GreaterThanOrEqualTo(x => x.PrecioMinimo)
            .WithMessage("El máximo no puede ser menor que el mínimo.");

        // La mediana adentro del rango. Fuera de él, el lote está mal armado y guardarlo
        // sería fabricar un precio de referencia que no describe ninguna muestra.
        RuleFor(x => x.PrecioMediano)
            .GreaterThanOrEqualTo(x => x.PrecioMinimo)
            .LessThanOrEqualTo(x => x.PrecioMaximo)
            .WithMessage("La mediana tiene que estar entre el mínimo y el máximo.");
    }
}
