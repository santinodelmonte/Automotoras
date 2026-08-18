using FluentValidation;

namespace AutomotoraSaaS.Core.Tenants;

/// <summary>
/// Un registro DNS que la automotora tiene que crear, con el porqué al lado.
/// </summary>
/// <remarks>
/// La explicación va en el DTO y no en el frontend porque quien la lee suele no ser quien
/// entiende de DNS: es el dueño de la automotora reenviándole esto a alguien que le maneja
/// el dominio. Cuanto más se explique solo el mensaje, menos viajes de ida y vuelta.
/// </remarks>
public sealed record RegistroDnsDto(string Tipo, string Nombre, string Valor, string Explicacion);

/// <summary>Un dominio propio y en qué punto del alta está.</summary>
/// <param name="Verificacion">El TXT que prueba la propiedad del dominio.</param>
/// <param name="ParaApuntarElTrafico">
/// Adónde apuntar el tráfico. Va vacío si la plataforma todavía no tiene configurado su
/// destino: es preferible no decir nada a dictar una IP inventada.
/// </param>
public sealed record DominioDto(
    int Id,
    string Dominio,
    string Estado,
    bool EsPrincipal,
    DateTime? VerificadoEn,
    DateTime? UltimaVerificacion,
    string? UltimoError,
    RegistroDnsDto Verificacion,
    IReadOnlyList<RegistroDnsDto> ParaApuntarElTrafico);

public sealed record AgregarDominioRequest(string Dominio);

public sealed class AgregarDominioRequestValidator : AbstractValidator<AgregarDominioRequest>
{
    public AgregarDominioRequestValidator()
    {
        RuleFor(x => x.Dominio)
            .NotEmpty().WithMessage("El dominio es obligatorio.")
            .Must(dominio => NombresDeDominio.EsValido(NombresDeDominio.Normalizar(dominio)))
            .WithMessage("Escribí el dominio solo, sin http:// ni barras. Por ejemplo: autosdelsur.com.uy");
    }
}

/// <summary>
/// Resultado de una operación sobre un dominio: o salió, o hay un motivo para mostrar.
/// </summary>
/// <remarks>
/// El motivo es texto para el usuario y no un código, porque todos los rechazos posibles
/// acá terminan en el mismo lugar de la pantalla y ninguno necesita que el frontend
/// bifurque.
/// </remarks>
public sealed record ResultadoDeDominio(DominioDto? Dominio, string? Rechazo)
{
    public static ResultadoDeDominio Ok(DominioDto dominio) => new(dominio, null);

    public static ResultadoDeDominio Rechazado(string motivo) => new(null, motivo);
}

/// <summary>Qué hizo la corrida de reverificación. Lo que el cron necesita para su log.</summary>
public sealed record ResumenDeVerificaciones(int Revisados, int Verificados, int Fallidos, int Caidos);
