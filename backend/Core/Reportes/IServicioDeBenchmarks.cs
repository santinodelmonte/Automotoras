namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Comparación anonimizada contra el resto de las automotoras.
/// </summary>
/// <remarks>
/// Es el único servicio del lado de tenant que lee datos de otras automotoras, y solo
/// devuelve agregados que cumplen los umbrales de <see cref="UmbralesDeBenchmark"/>. Vive
/// en su propio archivo justamente para que esa excepción se pueda auditar sola.
/// </remarks>
public interface IServicioDeBenchmarks
{
    Task<BenchmarkDto> CompararAsync(int dias, CancellationToken cancellationToken = default);
}
