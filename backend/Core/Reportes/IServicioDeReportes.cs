namespace AutomotoraSaaS.Core.Reportes;

/// <summary>
/// Los reportes de demanda de la automotora del request.
/// </summary>
/// <remarks>
/// No recibe el tenant por parámetro y no puede: lo pone el filtro global del
/// <c>DbContext</c>. Un servicio de reportes que aceptara un <c>tenantId</c> sería un
/// agujero esperando a que alguien le pase el de otro.
/// </remarks>
public interface IServicioDeReportes
{
    /// <summary>Vistas, consultas y señales por unidad, más la demanda insatisfecha.</summary>
    Task<ReporteDeDemandaDto> DemandaAsync(int dias, CancellationToken cancellationToken = default);

    /// <summary>Qué conviene traer, cruzando demanda insatisfecha con rotación histórica.</summary>
    Task<IReadOnlyList<SugerenciaDeCompraDto>> SugerenciasDeCompraAsync(
        int dias,
        CancellationToken cancellationToken = default);
}
