namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Se intentó escribir datos de un tenant distinto al del request, o escribir datos de
/// tenant sin ningún tenant resuelto.
/// </summary>
/// <remarks>
/// Nunca debería llegar al usuario como un error de validación: si se lanza, hay un bug
/// o un intento de manipulación. Que reviente es el comportamiento correcto.
/// </remarks>
public sealed class TenantIsolationException : InvalidOperationException
{
    public TenantIsolationException(string message) : base(message)
    {
    }
}
