namespace AutomotoraSaaS.Core.Tenants;

/// <summary>
/// Resuelve un dominio a las direcciones IP a las que apunta.
/// </summary>
/// <remarks>
/// Es una interfaz y no una llamada directa a <c>Dns</c> por dos motivos que valen lo
/// mismo: los tests no pueden depender de que exista internet ni de que un dominio real
/// siga apuntando a donde apuntaba, y una consulta de DNS es una llamada saliente que
/// conviene poder cambiar por otra implementación el día que el hosting cambie.
/// </remarks>
public interface IResolvedorDeDns
{
    /// <summary>
    /// Las IP a las que resuelve el dominio, o una lista vacía si no resuelve a ninguna.
    /// </summary>
    /// <remarks>
    /// No lanza cuando el dominio no existe: "no resuelve" es una respuesta esperable de
    /// esta consulta —es, de hecho, la respuesta más común mientras el dueño todavía no
    /// tocó su DNS— y no una falla del sistema.
    /// </remarks>
    Task<IReadOnlyList<string>> DireccionesDeAsync(string dominio, CancellationToken cancellationToken = default);
}

/// <summary>
/// Por qué un dominio quedó verificado o no.
/// </summary>
public enum ResultadoDeVerificacion
{
    /// <summary>El dominio apunta a la aplicación. Queda habilitado para servir el sitio.</summary>
    Verificado = 1,

    /// <summary>El dominio no resuelve a ninguna dirección: falta cargar el registro.</summary>
    NoResuelve = 2,

    /// <summary>Resuelve, pero a otro lado. El registro está, apuntando a otra parte.</summary>
    ApuntaAOtroLado = 3,

    /// <summary>La automotora no tiene dominio propio cargado.</summary>
    SinDominio = 4,

    /// <summary>
    /// No hay IP declarada contra la cual comparar, así que no se puede verificar nada.
    /// </summary>
    /// <remarks>
    /// Falla cerrado. Sin este caso, una configuración incompleta haría que la
    /// comparación no encuentre diferencias y dé cualquier dominio por bueno, que es
    /// exactamente lo contrario de lo que esta verificación existe para impedir.
    /// </remarks>
    SinIpsDeclaradas = 5,
}

/// <summary>
/// Cómo se verifica un dominio propio antes de dejarlo servir el sitio de una automotora.
/// </summary>
/// <remarks>
/// Se comprueba que el dominio apunte a la aplicación, y no un registro TXT con un token.
/// Es más simple y prueba lo mismo que hace falta: para apuntar un dominio a estas IP hay
/// que controlar su DNS, que es la definición práctica de ser su dueño. Y de yapa
/// comprueba la condición que igual tiene que cumplirse para que el sitio funcione — un
/// dominio verificado por TXT pero mal apuntado quedaría "verificado" y roto.
/// <para>
/// Por qué existe la verificación: sin ella, cualquier automotora puede escribir el
/// dominio de otra empresa en su configuración y quedárselo. Mientras ese dominio no
/// apunte acá no pasa nada visible, pero el día que apunte —porque su verdadero dueño lo
/// contrata— el tráfico entra al sitio de quien lo reservó primero.
/// </para>
/// </remarks>
public static class VerificacionDeDominio
{
    /// <summary>Clave de configuración con las IP públicas de la aplicación.</summary>
    public const string ClaveDeIps = "Deploy:IpsPublicas";

    /// <summary>
    /// Decide el resultado a partir de las IP a las que resuelve el dominio.
    /// </summary>
    /// <remarks>
    /// Alcanza con que una de las IP coincida. Un dominio puede resolver a varias —balanceo,
    /// IPv4 e IPv6, un CDN adelante— y exigir que todas sean nuestras rechazaría
    /// configuraciones correctas.
    /// </remarks>
    public static ResultadoDeVerificacion Evaluar(
        string? dominio,
        IReadOnlyCollection<string> resueltas,
        IReadOnlyCollection<string> declaradas)
    {
        ArgumentNullException.ThrowIfNull(resueltas);
        ArgumentNullException.ThrowIfNull(declaradas);

        if (string.IsNullOrWhiteSpace(dominio))
        {
            return ResultadoDeVerificacion.SinDominio;
        }

        if (declaradas.Count == 0)
        {
            return ResultadoDeVerificacion.SinIpsDeclaradas;
        }

        if (resueltas.Count == 0)
        {
            return ResultadoDeVerificacion.NoResuelve;
        }

        var coincide = resueltas.Any(ip => declaradas.Contains(ip, StringComparer.OrdinalIgnoreCase));

        return coincide ? ResultadoDeVerificacion.Verificado : ResultadoDeVerificacion.ApuntaAOtroLado;
    }

    /// <summary>Explicación para mostrarle a quien está configurando el dominio.</summary>
    public static string Explicacion(ResultadoDeVerificacion resultado) => resultado switch
    {
        ResultadoDeVerificacion.Verificado => "El dominio apunta a la aplicación y ya sirve el sitio.",
        ResultadoDeVerificacion.NoResuelve =>
            "El dominio todavía no resuelve a ninguna dirección. Cargá el registro A en el DNS y "
            + "volvé a intentar: la propagación puede tardar unas horas.",
        ResultadoDeVerificacion.ApuntaAOtroLado =>
            "El dominio resuelve, pero a otro servidor. Revisá que el registro A apunte a la "
            + "dirección de la aplicación.",
        ResultadoDeVerificacion.SinDominio => "La automotora no tiene un dominio propio cargado.",
        ResultadoDeVerificacion.SinIpsDeclaradas =>
            "No hay IP públicas declaradas en la configuración, así que no hay contra qué "
            + "comparar. Definí Deploy:IpsPublicas.",
        _ => "No se pudo verificar el dominio.",
    };
}
