using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Core.Analitica;

/// <summary>
/// Qué cuenta como una consulta.
/// </summary>
/// <remarks>
/// Está en un solo lugar y no repetido en cada reporte a propósito: el día que se agregue
/// un canal de contacto —un formulario, un clic en el mail— el tablero y los reportes
/// tienen que empezar a contarlo el mismo día. Dos definiciones de "consulta" que se
/// separan sin que nadie lo note es la forma más barata de que dos pantallas del mismo
/// panel muestren números distintos y ninguna sea confiable.
/// </remarks>
public static class EventosDeContacto
{
    /// <summary>
    /// Un clic en WhatsApp o en el teléfono. Es lo más cerca de una intención de compra
    /// que se puede medir sin que el comprador escriba nada.
    /// </summary>
    public static readonly TipoEvento[] Tipos = [TipoEvento.ClickWhatsapp, TipoEvento.ClickTelefono];
}
