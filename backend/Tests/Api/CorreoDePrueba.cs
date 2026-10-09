using System.Collections.Concurrent;
using AutomotoraSaaS.Core.Planes;

namespace AutomotoraSaaS.Tests.Api;

/// <summary>Correo en memoria: guarda lo que se mandó y no sale a ningún lado.</summary>
public sealed class CorreoDePrueba : INotificadorPorCorreo
{
    public sealed record Mensaje(IReadOnlyList<string> Para, string Asunto, string Cuerpo);

    public ConcurrentQueue<Mensaje> Enviados { get; } = new();

    /// <summary>Destinatarios para los que el envío falla, para probar los reintentos.</summary>
    public ConcurrentDictionary<string, bool> Fallan { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Configurado => true;

    public Task EnviarAsync(IReadOnlyList<string> para, string asunto, string cuerpo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(para);

        if (para.Any(Fallan.ContainsKey))
        {
            throw new InvalidOperationException("El servidor de correo de prueba rechazó el envío.");
        }

        Enviados.Enqueue(new Mensaje(para, asunto, cuerpo));
        return Task.CompletedTask;
    }

    public IReadOnlyList<Mensaje> Para(string email)
        => Enviados.Where(m => m.Para.Contains(email, StringComparer.OrdinalIgnoreCase)).ToList();
}
