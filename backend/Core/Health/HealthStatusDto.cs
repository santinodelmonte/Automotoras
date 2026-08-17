namespace AutomotoraSaaS.Core.Health;

/// <summary>
/// Respuesta del endpoint de health check.
/// </summary>
/// <param name="Status">Estado del servicio. "ok" cuando la API responde.</param>
/// <param name="Timestamp">Momento de la respuesta, en UTC.</param>
public sealed record HealthStatusDto(string Status, DateTimeOffset Timestamp);
