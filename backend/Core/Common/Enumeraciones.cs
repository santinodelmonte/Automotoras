namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Los enums viajan por la API como su nombre, no como su número.
/// </summary>
/// <remarks>
/// Un JSON que dice <c>"combustible": "Diesel"</c> se lee, se debuggea y sobrevive a que
/// alguien agregue un valor en el medio del enum. Uno que dice <c>2</c> no: el día que
/// los números se reordenen, todos los clientes ya publicados quedan interpretando otra
/// cosa. Adentro se siguen persistiendo como <c>int</c>, que es lo correcto para la base.
/// </remarks>
public static class Enumeraciones
{
    /// <summary>
    /// <c>true</c> si el texto nombra un valor real del enum. Descarta los números
    /// sueltos: <c>Enum.TryParse</c> acepta "37" y devuelve un enum que no existe.
    /// </summary>
    public static bool EsValido<T>(string? valor) where T : struct, Enum
        => !string.IsNullOrWhiteSpace(valor)
           && Enum.TryParse<T>(valor, ignoreCase: true, out var parseado)
           && Enum.IsDefined(parseado);

    /// <summary>
    /// Convierte un texto ya validado. Lanza si no es válido: llegar acá con basura es un
    /// bug del validador, no una entrada del usuario.
    /// </summary>
    public static T Parsear<T>(string valor) where T : struct, Enum
        => Enum.Parse<T>(valor, ignoreCase: true);

    /// <summary>Como <see cref="Parsear{T}"/>, pero devuelve <c>null</c> si el texto no sirve.</summary>
    public static T? ParsearOpcional<T>(string? valor) where T : struct, Enum
        => EsValido<T>(valor) ? Enum.Parse<T>(valor!, ignoreCase: true) : null;

    /// <summary>Todos los nombres del enum, para alimentar los selects del formulario.</summary>
    public static IReadOnlyList<string> Nombres<T>() where T : struct, Enum
        => Enum.GetNames<T>();
}
