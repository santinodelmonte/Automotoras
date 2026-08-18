namespace AutomotoraSaaS.Core.Common;

/// <summary>
/// Una página de resultados con el total, para que el cliente pueda pintar el paginador.
/// </summary>
public sealed record PaginaDe<T>(IReadOnlyList<T> Items, int Total, int Pagina, int PorPagina)
{
    public int TotalDePaginas => PorPagina <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PorPagina);

    public static PaginaDe<T> Vacia(int pagina, int porPagina) => new([], 0, pagina, porPagina);
}

/// <summary>
/// Límites de paginación, compartidos entre el panel y el sitio público.
/// </summary>
/// <remarks>
/// El tope existe para que nadie pida diez mil vehículos en un request. No es una
/// preferencia de UI: es lo que evita que un solo request de un scraper tumbe la API
/// compartida por todos los tenants.
/// </remarks>
public static class Paginacion
{
    public const int PorPaginaPorDefecto = 12;
    public const int PorPaginaMaximo = 60;
    public const int PrimeraPagina = 1;

    public static int NormalizarPagina(int pagina) => pagina < PrimeraPagina ? PrimeraPagina : pagina;

    public static int NormalizarPorPagina(int porPagina) => porPagina switch
    {
        <= 0 => PorPaginaPorDefecto,
        > PorPaginaMaximo => PorPaginaMaximo,
        _ => porPagina,
    };
}
