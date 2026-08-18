using AutomotoraSaaS.Core.Entities;

namespace AutomotoraSaaS.Core.Catalogo;

/// <summary>Proyección de las solicitudes de alta de modelo.</summary>
public static class MapeosDeSolicitud
{
    public static SolicitudModeloDto ADto(this SolicitudModelo solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        return new SolicitudModeloDto(
            solicitud.Id,
            solicitud.MarcaId,
            solicitud.Marca?.Nombre ?? string.Empty,
            solicitud.NombreModelo,
            solicitud.Carroceria.ToString(),
            solicitud.Estado.ToString(),
            solicitud.SolicitadaPor?.Nombre ?? string.Empty,
            solicitud.CreatedAt,
            solicitud.ResueltaEn,
            solicitud.NotaResolucion,
            solicitud.ModeloCreadoId);
    }
}
