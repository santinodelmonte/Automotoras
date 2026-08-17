namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// La entidad registra cuándo se creó. El <c>DbContext</c> la sella al insertar.
/// </summary>
/// <remarks>
/// No es solo comodidad: MySQL rechaza el valor por defecto de <see cref="DateTime"/>
/// (año 1), así que una entidad sin sellar directamente no persiste.
/// </remarks>
public interface ICreatedAt
{
    /// <summary>UTC.</summary>
    DateTime CreatedAt { get; set; }
}

/// <summary>
/// La entidad registra cuándo se modificó por última vez. El <c>DbContext</c> la sella
/// al insertar y al actualizar.
/// </summary>
public interface IUpdatedAt
{
    /// <summary>UTC.</summary>
    DateTime UpdatedAt { get; set; }
}
