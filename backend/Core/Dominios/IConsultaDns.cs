namespace AutomotoraSaaS.Core.Dominios;

/// <summary>
/// Lee registros TXT del DNS público.
/// </summary>
/// <remarks>
/// Es una interfaz y no una llamada directa porque la verificación de dominios se prueba
/// entera con tests: sin esto, cada test necesitaría un dominio real con un TXT real, y la
/// suite pasaría a depender de internet y de que alguien no toque una zona DNS.
/// </remarks>
public interface IConsultaDns
{
    /// <summary>
    /// Valores TXT publicados en ese nombre. Lista vacía si no hay ninguno o si el nombre no
    /// existe: para lo que se usa acá, "no existe" y "existe sin TXT" son el mismo caso.
    /// </summary>
    /// <exception cref="ConsultaDnsFallidaException">
    /// Si no se pudo consultar. Es distinto de no encontrar nada: un timeout no prueba que
    /// el dominio no sea de quien dice, y no puede contar como fallo de verificación.
    /// </exception>
    Task<IReadOnlyList<string>> TxtAsync(string nombre, CancellationToken cancellationToken = default);
}

/// <summary>
/// El DNS no contestó. No confundir con contestar que no hay nada.
/// </summary>
public sealed class ConsultaDnsFallidaException : Exception
{
    public ConsultaDnsFallidaException(string message) : base(message)
    {
    }

    public ConsultaDnsFallidaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConsultaDnsFallidaException()
    {
    }
}
