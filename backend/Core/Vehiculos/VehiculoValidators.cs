using AutomotoraSaaS.Core.Common;
using AutomotoraSaaS.Core.Enums;
using FluentValidation;

namespace AutomotoraSaaS.Core.Vehiculos;

/// <summary>
/// Rangos aceptables de un vehículo. Son cotas de sanidad, no reglas de negocio: paran
/// el dedazo (un año 20255, un kilometraje de nueve millones) antes de que ensucie la
/// analítica, que es el producto.
/// </summary>
public static class LimitesDeVehiculo
{
    public const int AnioMinimo = 1950;
    public const int KilometrajeMaximo = 2_000_000;
    public const decimal PrecioMaximo = 99_999_999m;
    public const int PuertasMinimo = 2;
    public const int PuertasMaximo = 7;

    /// <summary>
    /// Los modelos salen a la venta como "año que viene", así que el tope es el próximo.
    /// Se calcula, no se hardcodea: una constante acá es un bug con fecha de vencimiento.
    /// </summary>
    public static int AnioMaximo(TimeProvider reloj) => reloj.GetUtcNow().Year + 1;
}

public sealed class GuardarVehiculoRequestValidator : AbstractValidator<GuardarVehiculoRequest>
{
    public GuardarVehiculoRequestValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        var anioMaximo = LimitesDeVehiculo.AnioMaximo(reloj);

        RuleFor(x => x.ModeloId)
            .GreaterThan(0).WithMessage("Elegí el modelo.");

        RuleFor(x => x.VersionId)
            .GreaterThan(0).When(x => x.VersionId is not null)
            .WithMessage("La versión no es válida.");

        RuleFor(x => x.Anio)
            .InclusiveBetween(LimitesDeVehiculo.AnioMinimo, anioMaximo)
            .WithMessage($"El año tiene que estar entre {LimitesDeVehiculo.AnioMinimo} y {anioMaximo}.");

        RuleFor(x => x.Kilometraje)
            .InclusiveBetween(0, LimitesDeVehiculo.KilometrajeMaximo)
            .WithMessage("El kilometraje no parece real.");

        RuleFor(x => x.Combustible)
            .Must(Enumeraciones.EsValido<Combustible>).WithMessage("El combustible no es válido.");

        RuleFor(x => x.Transmision)
            .Must(Enumeraciones.EsValido<Transmision>).WithMessage("La transmisión no es válida.");

        RuleFor(x => x.Moneda)
            .Must(Enumeraciones.EsValido<Moneda>).WithMessage("La moneda no es válida.");

        RuleFor(x => x.Precio)
            .GreaterThan(0m).WithMessage("El precio es obligatorio.")
            .LessThanOrEqualTo(LimitesDeVehiculo.PrecioMaximo);

        RuleFor(x => x.PrecioCosto)
            .GreaterThan(0m).LessThanOrEqualTo(LimitesDeVehiculo.PrecioMaximo)
            .When(x => x.PrecioCosto is not null)
            .WithMessage("El precio de costo no es válido.");

        RuleFor(x => x.Puertas)
            .InclusiveBetween(LimitesDeVehiculo.PuertasMinimo, LimitesDeVehiculo.PuertasMaximo)
            .When(x => x.Puertas is not null)
            .WithMessage("La cantidad de puertas no es válida.");

        RuleFor(x => x.Color).MaximumLength(40);
        RuleFor(x => x.Motor).MaximumLength(60);
        RuleFor(x => x.Descripcion).MaximumLength(4000);

        // Publicar con fecha futura dejaría un vehículo con días en góndola negativos, y
        // ese número alimenta los reportes.
        RuleFor(x => x.FechaPublicacion)
            .LessThanOrEqualTo(_ => reloj.GetUtcNow().UtcDateTime.AddDays(1))
            .When(x => x.FechaPublicacion is not null)
            .WithMessage("La fecha de publicación no puede ser futura.");
    }
}

public sealed class CambiarEstadoRequestValidator : AbstractValidator<CambiarEstadoRequest>
{
    public CambiarEstadoRequestValidator(TimeProvider reloj)
    {
        ArgumentNullException.ThrowIfNull(reloj);

        RuleFor(x => x.Estado)
            .Must(Enumeraciones.EsValido<EstadoVehiculo>).WithMessage("El estado no es válido.");

        // Sin fecha y precio de venta, marcar vendido no deja registro de nada. Y sin
        // ese registro no hay días en góndola ni margen: la mitad de para qué existe el
        // producto se pierde en el momento exacto en que se genera el dato.
        RuleFor(x => x.FechaVenta)
            .NotNull()
            .When(EsVenta)
            .WithMessage("Al marcar vendido hay que indicar la fecha de venta.");

        RuleFor(x => x.PrecioVenta)
            .NotNull().GreaterThan(0m)
            .When(EsVenta)
            .WithMessage("Al marcar vendido hay que indicar el precio de venta.");

        RuleFor(x => x.FechaVenta)
            .LessThanOrEqualTo(_ => reloj.GetUtcNow().UtcDateTime.AddDays(1))
            .When(x => x.FechaVenta is not null)
            .WithMessage("La fecha de venta no puede ser futura.");
    }

    private static bool EsVenta(CambiarEstadoRequest request)
        => Enumeraciones.ParsearOpcional<EstadoVehiculo>(request.Estado) == EstadoVehiculo.Vendido;
}

public sealed class ReordenarFotosRequestValidator : AbstractValidator<ReordenarFotosRequest>
{
    public ReordenarFotosRequestValidator()
    {
        RuleFor(x => x.FotoIds)
            .NotEmpty().WithMessage("Mandá el orden de las fotos.");

        RuleFor(x => x.FotoIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .When(x => x.FotoIds is not null)
            .WithMessage("Hay fotos repetidas en el orden.");
    }
}

public sealed class FiltrosPublicosDeVehiculosValidator : AbstractValidator<FiltrosPublicosDeVehiculos>
{
    public FiltrosPublicosDeVehiculosValidator()
    {
        RuleFor(x => x.Combustible)
            .Must(Enumeraciones.EsValido<Combustible>)
            .When(x => x.Combustible is not null)
            .WithMessage("El combustible no es válido.");

        RuleFor(x => x.Transmision)
            .Must(Enumeraciones.EsValido<Transmision>)
            .When(x => x.Transmision is not null)
            .WithMessage("La transmisión no es válida.");

        RuleFor(x => x.Carroceria)
            .Must(Enumeraciones.EsValido<Carroceria>)
            .When(x => x.Carroceria is not null)
            .WithMessage("La carrocería no es válida.");

        RuleFor(x => x.Moneda)
            .Must(Enumeraciones.EsValido<Moneda>)
            .When(x => x.Moneda is not null)
            .WithMessage("La moneda no es válida.");

        // Un rango de precios sin moneda mezcla dólares con pesos y devuelve cualquier
        // cosa. Es mejor pedir la moneda que devolver un listado sin sentido.
        RuleFor(x => x.Moneda)
            .NotNull()
            .When(x => x.PrecioDesde is not null || x.PrecioHasta is not null)
            .WithMessage("Para filtrar por precio hay que elegir la moneda.");

        RuleFor(x => x.AnioHasta)
            .GreaterThanOrEqualTo(x => x.AnioDesde!.Value)
            .When(x => x.AnioDesde is not null && x.AnioHasta is not null)
            .WithMessage("El rango de años está al revés.");

        RuleFor(x => x.PrecioHasta)
            .GreaterThanOrEqualTo(x => x.PrecioDesde!.Value)
            .When(x => x.PrecioDesde is not null && x.PrecioHasta is not null)
            .WithMessage("El rango de precios está al revés.");

        RuleFor(x => x.KmHasta)
            .GreaterThanOrEqualTo(x => x.KmDesde!.Value)
            .When(x => x.KmDesde is not null && x.KmHasta is not null)
            .WithMessage("El rango de kilometraje está al revés.");

        RuleFor(x => x.SessionId).MaximumLength(64);
    }
}
