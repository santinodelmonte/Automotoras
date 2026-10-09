using AutomotoraSaaS.Core.Enums;

namespace AutomotoraSaaS.Infrastructure.Persistence;

/// <summary>
/// Fotos de catálogo para el stock de desarrollo, una lista por modelo real.
/// </summary>
/// <remarks>
/// Las fotos del seed no son decoración. Un placeholder genérico —un paisaje, un color
/// plano— alcanza para comprobar que la galería maqueta bien, pero no para mirar la
/// pantalla y saber si el producto se entiende: el ojo descarta la ficha entera antes de
/// leer el precio. Por eso cada modelo del catálogo tiene sus propias fotos y una Hilux se
/// ve como una Hilux.
/// <para>
/// Son archivos de Wikimedia Commons, de licencia libre y URL estable, referenciados y no
/// copiados: el seed no sube binarios a ningún storage. Son datos de desarrollo; el stock
/// real lo cargan las automotoras con sus propias fotos.
/// </para>
/// <para>
/// La tabla es estática a propósito. Salir a buscar imágenes durante el arranque ataría el
/// seed a que haya red y a que un servicio ajeno esté arriba, y un seed que a veces falla
/// es peor que uno pobre.
/// </para>
/// </remarks>
internal static class FotosDeCatalogo
{
    /// <summary>
    /// Una foto en sus dos tamaños: la de la ficha y la de la grilla.
    /// </summary>
    /// <remarks>
    /// Los dos vienen guardados y no se derivan uno del otro. Commons solo sirve los
    /// anchos que tiene generados para cada archivo —pedirle uno cualquiera devuelve 400—
    /// y cuáles son cambia de imagen en imagen. Una regla que reescriba el ancho en la URL
    /// parece funcionar mientras se prueba con una foto y deja la grilla entera rota.
    /// </remarks>
    internal sealed record Foto(string Url, string UrlMiniatura);

    internal sealed record FotosDelModelo(Carroceria Carroceria, Foto[] Fotos);

    /// <summary>
    /// Las fotos de un modelo. Si el modelo no está en la tabla —lo dio de alta un
    /// SuperAdmin después— cae en otro de la misma carrocería: una SUV desconocida
    /// mostrada como SUV es un error que no se nota, y mostrada como sedán sí.
    /// </summary>
    public static Foto[] Para(string clave, Carroceria carroceria, Random azar)
    {
        ArgumentNullException.ThrowIfNull(azar);

        if (PorModelo.TryGetValue(clave, out var fotos))
        {
            return fotos.Fotos;
        }

        var deLaMismaCarroceria = PorModelo.Values
            .Where(f => f.Carroceria == carroceria)
            .ToList();

        return deLaMismaCarroceria.Count > 0
            ? deLaMismaCarroceria[azar.Next(deLaMismaCarroceria.Count)].Fotos
            : PorModelo.Values.First().Fotos;
    }

    private static readonly Dictionary<string, FotosDelModelo> PorModelo = new()
    {
        ["BYD Dolphin"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/BYD_Dolphin_IAA_2023_1X7A0634.jpg/1280px-BYD_Dolphin_IAA_2023_1X7A0634.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/BYD_Dolphin_IAA_2023_1X7A0634.jpg/500px-BYD_Dolphin_IAA_2023_1X7A0634.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9f/BYD_Dolphin_%28Global_version%29_IMG_9507.jpg/1280px-BYD_Dolphin_%28Global_version%29_IMG_9507.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9f/BYD_Dolphin_%28Global_version%29_IMG_9507.jpg/500px-BYD_Dolphin_%28Global_version%29_IMG_9507.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/56/BYD_Atto_2_DSC_8763.jpg/1280px-BYD_Atto_2_DSC_8763.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/56/BYD_Atto_2_DSC_8763.jpg/500px-BYD_Atto_2_DSC_8763.jpg"),
        ]),
        ["BYD Song Plus"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/%D0%90%D0%BB%D0%BC%D0%B0%D1%82%D1%8B%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A8%D0%B5%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE.jpg/1280px-%D0%90%D0%BB%D0%BC%D0%B0%D1%82%D1%8B%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A8%D0%B5%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/%D0%90%D0%BB%D0%BC%D0%B0%D1%82%D1%8B%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A8%D0%B5%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE.jpg/500px-%D0%90%D0%BB%D0%BC%D0%B0%D1%82%D1%8B%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A8%D0%B5%D0%B2%D1%87%D0%B5%D0%BD%D0%BA%D0%BE.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9b/%D0%91%D1%83%D1%85%D0%B0%D1%80%D0%B0%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A0%D1%83%D1%88%D0%BD%D0%BE%D0%B8-%D0%9D%D0%B0%D0%BC%D0%B0%D0%B7%D0%B3%D0%BE%D1%85%D0%B5.jpg/1280px-%D0%91%D1%83%D1%85%D0%B0%D1%80%D0%B0%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A0%D1%83%D1%88%D0%BD%D0%BE%D0%B8-%D0%9D%D0%B0%D0%BC%D0%B0%D0%B7%D0%B3%D0%BE%D1%85%D0%B5.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9b/%D0%91%D1%83%D1%85%D0%B0%D1%80%D0%B0%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A0%D1%83%D1%88%D0%BD%D0%BE%D0%B8-%D0%9D%D0%B0%D0%BC%D0%B0%D0%B7%D0%B3%D0%BE%D1%85%D0%B5.jpg/500px-%D0%91%D1%83%D1%85%D0%B0%D1%80%D0%B0%2C_BYD_Song_Plus_%D0%BD%D0%B0_%D0%A0%D1%83%D1%88%D0%BD%D0%BE%D0%B8-%D0%9D%D0%B0%D0%BC%D0%B0%D0%B7%D0%B3%D0%BE%D1%85%D0%B5.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/08/%D0%A1%D1%83%D0%BC%D0%B8%D1%82%D0%B0%D0%BD%2C_BYD_Song_Plus.jpg/1280px-%D0%A1%D1%83%D0%BC%D0%B8%D1%82%D0%B0%D0%BD%2C_BYD_Song_Plus.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/08/%D0%A1%D1%83%D0%BC%D0%B8%D1%82%D0%B0%D0%BD%2C_BYD_Song_Plus.jpg/500px-%D0%A1%D1%83%D0%BC%D0%B8%D1%82%D0%B0%D0%BD%2C_BYD_Song_Plus.jpg"),
        ]),
        ["BYD Yuan Plus"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/78/BYD_Atto_3_1X7A6495.jpg/1280px-BYD_Atto_3_1X7A6495.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/78/BYD_Atto_3_1X7A6495.jpg/500px-BYD_Atto_3_1X7A6495.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/10/BYD_Atto_3_1X7A6494.jpg/1280px-BYD_Atto_3_1X7A6494.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/10/BYD_Atto_3_1X7A6494.jpg/500px-BYD_Atto_3_1X7A6494.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e3/BYD_Yuan_Plus_II_007.jpg/1280px-BYD_Yuan_Plus_II_007.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e3/BYD_Yuan_Plus_II_007.jpg/500px-BYD_Yuan_Plus_II_007.jpg"),
        ]),
        ["Chery Arrizo 5"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/db/Chery_Arrizo_5_Shishi_01_2022-10-12.jpg/1280px-Chery_Arrizo_5_Shishi_01_2022-10-12.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/db/Chery_Arrizo_5_Shishi_01_2022-10-12.jpg/500px-Chery_Arrizo_5_Shishi_01_2022-10-12.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Chery_Arrizo_5_Shishi_02_2022-10-12.jpg/1280px-Chery_Arrizo_5_Shishi_02_2022-10-12.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Chery_Arrizo_5_Shishi_02_2022-10-12.jpg/500px-Chery_Arrizo_5_Shishi_02_2022-10-12.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/9/91/Chery_Arrizo_5_GT_001.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/91/Chery_Arrizo_5_GT_001.jpg/500px-Chery_Arrizo_5_GT_001.jpg"),
        ]),
        ["Chery Tiggo 2"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6f/MVM_X22_01_2024-07-31.jpg/1280px-MVM_X22_01_2024-07-31.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6f/MVM_X22_01_2024-07-31.jpg/500px-MVM_X22_01_2024-07-31.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/31/MVM_X22_02_2024-07-31.jpg/1280px-MVM_X22_02_2024-07-31.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/31/MVM_X22_02_2024-07-31.jpg/500px-MVM_X22_02_2024-07-31.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d2/Chery_Tiggo_8_2.jpg/1280px-Chery_Tiggo_8_2.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d2/Chery_Tiggo_8_2.jpg/500px-Chery_Tiggo_8_2.jpg"),
        ]),
        // Commons no tiene fotos del Tiggo 4. Se usan SUV Chery de producción de la misma
        // familia: lo que había con el nombre exacto eran prototipos de salón, y un
        // prototipo en una ficha de usado se lee como un error del sistema.
        ["Chery Tiggo 4"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/50/Chery_Tiggo_7_II_IMG001.jpg/1280px-Chery_Tiggo_7_II_IMG001.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/50/Chery_Tiggo_7_II_IMG001.jpg/500px-Chery_Tiggo_7_II_IMG001.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Chery_Tiggo_7_II_IMG002.jpg/1280px-Chery_Tiggo_7_II_IMG002.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Chery_Tiggo_7_II_IMG002.jpg/500px-Chery_Tiggo_7_II_IMG002.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f3/Chery_Tiggo_8_Plus_003.jpg/1280px-Chery_Tiggo_8_Plus_003.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f3/Chery_Tiggo_8_Plus_003.jpg/500px-Chery_Tiggo_8_Plus_003.jpg"),
        ]),
        ["Chery Tiggo 7"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/42/Chery_Tiggo_7_Back_IMG_0391.jpg/1280px-Chery_Tiggo_7_Back_IMG_0391.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/42/Chery_Tiggo_7_Back_IMG_0391.jpg/500px-Chery_Tiggo_7_Back_IMG_0391.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/2/20/Chery_Tiggo_7.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/20/Chery_Tiggo_7.jpg/500px-Chery_Tiggo_7.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Chery_Tiggo_7_II_IMG005.jpg/1280px-Chery_Tiggo_7_II_IMG005.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c5/Chery_Tiggo_7_II_IMG005.jpg/500px-Chery_Tiggo_7_II_IMG005.jpg"),
        ]),
        ["Chevrolet Onix"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Chevrolet_Onix_008.jpg/1280px-Chevrolet_Onix_008.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Chevrolet_Onix_008.jpg/500px-Chevrolet_Onix_008.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/69/Chevrolet_Onix_011.jpg/1280px-Chevrolet_Onix_011.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/69/Chevrolet_Onix_011.jpg/500px-Chevrolet_Onix_011.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/Chevrolet_Onix_009.jpg/1280px-Chevrolet_Onix_009.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/Chevrolet_Onix_009.jpg/500px-Chevrolet_Onix_009.jpg"),
        ]),
        ["Chevrolet Onix Plus"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/2022_Chevrolet_Onix_Plus_1.0_Premier_%28rear%29.jpg/1280px-2022_Chevrolet_Onix_Plus_1.0_Premier_%28rear%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/2022_Chevrolet_Onix_Plus_1.0_Premier_%28rear%29.jpg/500px-2022_Chevrolet_Onix_Plus_1.0_Premier_%28rear%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f1/2022_Chevrolet_Onix_Plus_1.0_Premier_%28side%29.jpg/1280px-2022_Chevrolet_Onix_Plus_1.0_Premier_%28side%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f1/2022_Chevrolet_Onix_Plus_1.0_Premier_%28side%29.jpg/500px-2022_Chevrolet_Onix_Plus_1.0_Premier_%28side%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2021_Chevrolet_Onix_Plus_1.2_LT.jpg/1280px-2021_Chevrolet_Onix_Plus_1.2_LT.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2021_Chevrolet_Onix_Plus_1.2_LT.jpg/500px-2021_Chevrolet_Onix_Plus_1.2_LT.jpg"),
        ]),
        ["Chevrolet S10"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/89/2019_Chevrolet_Colorado_LTZ.jpg/1280px-2019_Chevrolet_Colorado_LTZ.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/89/2019_Chevrolet_Colorado_LTZ.jpg/500px-2019_Chevrolet_Colorado_LTZ.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Chevrolet_Colorado_4x4_LTZ_2019.jpg/1280px-Chevrolet_Colorado_4x4_LTZ_2019.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Chevrolet_Colorado_4x4_LTZ_2019.jpg/500px-Chevrolet_Colorado_4x4_LTZ_2019.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/75/Chevrolet_Colorado_LTZ_2.8_TD_4x4_2018_%2842676645454%29.jpg/1280px-Chevrolet_Colorado_LTZ_2.8_TD_4x4_2018_%2842676645454%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/75/Chevrolet_Colorado_LTZ_2.8_TD_4x4_2018_%2842676645454%29.jpg/500px-Chevrolet_Colorado_LTZ_2.8_TD_4x4_2018_%2842676645454%29.jpg"),
        ]),
        ["Chevrolet Spin"] = new(Carroceria.Minivan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Chevrolet_Spin_20150814-DSC05636.JPG/1280px-Chevrolet_Spin_20150814-DSC05636.JPG",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c3/Chevrolet_Spin_20150814-DSC05636.JPG/500px-Chevrolet_Spin_20150814-DSC05636.JPG"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/7a/2013_Chevrolet_Spin_1.5_LTZ_%2820190616%29.jpg/1280px-2013_Chevrolet_Spin_1.5_LTZ_%2820190616%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7a/2013_Chevrolet_Spin_1.5_LTZ_%2820190616%29.jpg/500px-2013_Chevrolet_Spin_1.5_LTZ_%2820190616%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/80/2019_Chevrolet_Spin_%28Arequipa%2C_Per%C3%BA%29.jpg/1280px-2019_Chevrolet_Spin_%28Arequipa%2C_Per%C3%BA%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/80/2019_Chevrolet_Spin_%28Arequipa%2C_Per%C3%BA%29.jpg/500px-2019_Chevrolet_Spin_%28Arequipa%2C_Per%C3%BA%29.jpg"),
        ]),
        ["Chevrolet Tracker"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/90/Chevrolet_Tracker_003.jpg/1280px-Chevrolet_Tracker_003.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/90/Chevrolet_Tracker_003.jpg/500px-Chevrolet_Tracker_003.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/Chevrolet_Tracker_004.jpg/1280px-Chevrolet_Tracker_004.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/Chevrolet_Tracker_004.jpg/500px-Chevrolet_Tracker_004.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/52/Chevrolet_Tracker%2C_Rear_Left%2C_09-19-2021.jpg/1280px-Chevrolet_Tracker%2C_Rear_Left%2C_09-19-2021.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/52/Chevrolet_Tracker%2C_Rear_Left%2C_09-19-2021.jpg/500px-Chevrolet_Tracker%2C_Rear_Left%2C_09-19-2021.jpg"),
        ]),
        ["Citroën Berlingo"] = new(Carroceria.Van, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/Citro%C3%ABn_%C3%AB-Berlingo_Automesse_Ludwigsburg_2024_IMG_1657.jpg/1280px-Citro%C3%ABn_%C3%AB-Berlingo_Automesse_Ludwigsburg_2024_IMG_1657.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/Citro%C3%ABn_%C3%AB-Berlingo_Automesse_Ludwigsburg_2024_IMG_1657.jpg/500px-Citro%C3%ABn_%C3%AB-Berlingo_Automesse_Ludwigsburg_2024_IMG_1657.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/85/Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0097.jpg/1280px-Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0097.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/85/Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0097.jpg/500px-Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0097.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0095.jpg/1280px-Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0095.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0095.jpg/500px-Citroen_e-Berlingo_Auto_Zuerich_2021_IMG_0095.jpg"),
        ]),
        ["Citroën C3"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/3a/2020_Citro%C3%ABn_C3_%283rd_generation%29_DSC_7374.jpg/1280px-2020_Citro%C3%ABn_C3_%283rd_generation%29_DSC_7374.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3a/2020_Citro%C3%ABn_C3_%283rd_generation%29_DSC_7374.jpg/500px-2020_Citro%C3%ABn_C3_%283rd_generation%29_DSC_7374.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/01/Citroen_C3_Aircross_%282017%29_Facelift_1X7A7060.jpg/1280px-Citroen_C3_Aircross_%282017%29_Facelift_1X7A7060.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/01/Citroen_C3_Aircross_%282017%29_Facelift_1X7A7060.jpg/500px-Citroen_C3_Aircross_%282017%29_Facelift_1X7A7060.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bf/Citroen_C3_Aircross_%282017%29_Facelift_1X7A7061.jpg/1280px-Citroen_C3_Aircross_%282017%29_Facelift_1X7A7061.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bf/Citroen_C3_Aircross_%282017%29_Facelift_1X7A7061.jpg/500px-Citroen_C3_Aircross_%282017%29_Facelift_1X7A7061.jpg"),
        ]),
        ["Citroën C4 Cactus"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/Citroen_C4_Cactus_Genf_2018.jpg/1280px-Citroen_C4_Cactus_Genf_2018.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/Citroen_C4_Cactus_Genf_2018.jpg/500px-Citroen_C4_Cactus_Genf_2018.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/71/Citroen_C4_Cactus_%282018%29_IMG_4062.jpg/1280px-Citroen_C4_Cactus_%282018%29_IMG_4062.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/71/Citroen_C4_Cactus_%282018%29_IMG_4062.jpg/500px-Citroen_C4_Cactus_%282018%29_IMG_4062.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/ee/Citroen_C4_Cactus_%282018%29_IMG_5510.jpg/1280px-Citroen_C4_Cactus_%282018%29_IMG_5510.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ee/Citroen_C4_Cactus_%282018%29_IMG_5510.jpg/500px-Citroen_C4_Cactus_%282018%29_IMG_5510.jpg"),
        ]),
        ["Fiat Argo"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/45/Fiat_Argo_test_drive_car_in_Punta_del_Este_%28front%29.jpg/1280px-Fiat_Argo_test_drive_car_in_Punta_del_Este_%28front%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/45/Fiat_Argo_test_drive_car_in_Punta_del_Este_%28front%29.jpg/500px-Fiat_Argo_test_drive_car_in_Punta_del_Este_%28front%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6b/FIAT_Argo.jpg/1280px-FIAT_Argo.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6b/FIAT_Argo.jpg/500px-FIAT_Argo.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/2023_Fiat_Argo_1.3_Drive_%28facelift%2C_Brazil%29.jpg/1280px-2023_Fiat_Argo_1.3_Drive_%28facelift%2C_Brazil%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/2023_Fiat_Argo_1.3_Drive_%28facelift%2C_Brazil%29.jpg/500px-2023_Fiat_Argo_1.3_Drive_%28facelift%2C_Brazil%29.jpg"),
        ]),
        ["Fiat Cronos"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/1/16/Fiat_Cronos_1.8_16V_E.Torq_Precision.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/16/Fiat_Cronos_1.8_16V_E.Torq_Precision.jpg/500px-Fiat_Cronos_1.8_16V_E.Torq_Precision.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/fa/2024_Fiat_Cronos_1.3_GSE_Precision.jpg/1280px-2024_Fiat_Cronos_1.3_GSE_Precision.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fa/2024_Fiat_Cronos_1.3_GSE_Precision.jpg/500px-2024_Fiat_Cronos_1.3_GSE_Precision.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b3/2024_Fiat_Cronos_1.3_GSE_S-Design_%28driver_door%29.jpg/1280px-2024_Fiat_Cronos_1.3_GSE_S-Design_%28driver_door%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b3/2024_Fiat_Cronos_1.3_GSE_S-Design_%28driver_door%29.jpg/500px-2024_Fiat_Cronos_1.3_GSE_S-Design_%28driver_door%29.jpg"),
        ]),
        ["Fiat Mobi"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/Fiat_Mobi.jpg/1280px-Fiat_Mobi.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/Fiat_Mobi.jpg/500px-Fiat_Mobi.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/Fiat_Mobi_002.jpg/1280px-Fiat_Mobi_002.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/Fiat_Mobi_002.jpg/500px-Fiat_Mobi_002.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/Fiat_Mobi_003.jpg/1280px-Fiat_Mobi_003.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/Fiat_Mobi_003.jpg/500px-Fiat_Mobi_003.jpg"),
        ]),
        ["Fiat Pulse"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/2c/2022_Fiat_Pulse_1.3_Drive.jpg/1280px-2022_Fiat_Pulse_1.3_Drive.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2c/2022_Fiat_Pulse_1.3_Drive.jpg/500px-2022_Fiat_Pulse_1.3_Drive.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/74/2023_Fiat_Pulse_Impetus_%28Colombia%29_front_view_01.jpg/1280px-2023_Fiat_Pulse_Impetus_%28Colombia%29_front_view_01.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/74/2023_Fiat_Pulse_Impetus_%28Colombia%29_front_view_01.jpg/500px-2023_Fiat_Pulse_Impetus_%28Colombia%29_front_view_01.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/2024_Fiat_Pulse_1.0_Turbo_200_Audace.jpg/1280px-2024_Fiat_Pulse_1.0_Turbo_200_Audace.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/2024_Fiat_Pulse_1.0_Turbo_200_Audace.jpg/500px-2024_Fiat_Pulse_1.0_Turbo_200_Audace.jpg"),
        ]),
        ["Fiat Strada"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/58/Fiat_Strada_Mk6_Volcano_in_Uruguay_-_rear.jpg/1280px-Fiat_Strada_Mk6_Volcano_in_Uruguay_-_rear.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/58/Fiat_Strada_Mk6_Volcano_in_Uruguay_-_rear.jpg/500px-Fiat_Strada_Mk6_Volcano_in_Uruguay_-_rear.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e9/Fiat_Strada_Mk6_Volcano_in_Uruguay_-_front.jpg/1280px-Fiat_Strada_Mk6_Volcano_in_Uruguay_-_front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e9/Fiat_Strada_Mk6_Volcano_in_Uruguay_-_front.jpg/500px-Fiat_Strada_Mk6_Volcano_in_Uruguay_-_front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9d/Fiat_Strada_Freedom_Mk6_with_topper_in_Montevideo.jpg/1280px-Fiat_Strada_Freedom_Mk6_with_topper_in_Montevideo.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9d/Fiat_Strada_Freedom_Mk6_with_topper_in_Montevideo.jpg/500px-Fiat_Strada_Freedom_Mk6_with_topper_in_Montevideo.jpg"),
        ]),
        ["Fiat Toro"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/FiatToro-jul2016.jpg/1280px-FiatToro-jul2016.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/FiatToro-jul2016.jpg/500px-FiatToro-jul2016.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/84/Fiat_Toro_Volcano_front.jpg/1280px-Fiat_Toro_Volcano_front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/84/Fiat_Toro_Volcano_front.jpg/500px-Fiat_Toro_Volcano_front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/a6/Fiat_Toro_2018_in_Punta_del_Este_%28front%29_01.jpg/1280px-Fiat_Toro_2018_in_Punta_del_Este_%28front%29_01.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a6/Fiat_Toro_2018_in_Punta_del_Este_%28front%29_01.jpg/500px-Fiat_Toro_2018_in_Punta_del_Este_%28front%29_01.jpg"),
        ]),
        ["Ford EcoSport"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/2018_Ford_Ecosport_ST-Line_TDCi_1.5.jpg/1280px-2018_Ford_Ecosport_ST-Line_TDCi_1.5.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/2018_Ford_Ecosport_ST-Line_TDCi_1.5.jpg/500px-2018_Ford_Ecosport_ST-Line_TDCi_1.5.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/96/Ford_EcoSport_Sollers.jpg/1280px-Ford_EcoSport_Sollers.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/96/Ford_EcoSport_Sollers.jpg/500px-Ford_EcoSport_Sollers.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/Ford_EcoSport_luggage_compartment.jpg/1280px-Ford_EcoSport_luggage_compartment.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/Ford_EcoSport_luggage_compartment.jpg/500px-Ford_EcoSport_luggage_compartment.jpg"),
        ]),
        ["Ford Ka"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d8/FordKa-3rageneracion-MardeCobo.jpg/1280px-FordKa-3rageneracion-MardeCobo.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d8/FordKa-3rageneracion-MardeCobo.jpg/500px-FordKa-3rageneracion-MardeCobo.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/0/09/Ford_Ka_Rear.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Ford_Ka_Rear.jpg/500px-Ford_Ka_Rear.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Ford_Ka_1.5_SE_2018.jpg/1280px-Ford_Ka_1.5_SE_2018.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Ford_Ka_1.5_SE_2018.jpg/500px-Ford_Ka_1.5_SE_2018.jpg"),
        ]),
        ["Ford Maverick"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/Ford_Maverick_Tremor_DSC_2885.jpg/1280px-Ford_Maverick_Tremor_DSC_2885.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/Ford_Maverick_Tremor_DSC_2885.jpg/500px-Ford_Maverick_Tremor_DSC_2885.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/a4/1972_Ford_Maverick_Sprint_%28616806978%29.jpg/1280px-1972_Ford_Maverick_Sprint_%28616806978%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a4/1972_Ford_Maverick_Sprint_%28616806978%29.jpg/500px-1972_Ford_Maverick_Sprint_%28616806978%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/85/Ford-Maverick-Grabber.jpg/1280px-Ford-Maverick-Grabber.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/85/Ford-Maverick-Grabber.jpg/500px-Ford-Maverick-Grabber.jpg"),
        ]),
        ["Ford Ranger"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7320.jpg/1280px-Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7320.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7320.jpg/500px-Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7320.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e8/Ford_Ranger_Raptor_-_20231003-P1003802.jpg/1280px-Ford_Ranger_Raptor_-_20231003-P1003802.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e8/Ford_Ranger_Raptor_-_20231003-P1003802.jpg/500px-Ford_Ranger_Raptor_-_20231003-P1003802.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7323.jpg/1280px-Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7323.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/57/Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7323.jpg/500px-Ford_Ranger_%28T6%2C_P703%29_Wildtrak_IMG_7323.jpg"),
        ]),
        ["Ford Territory"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Ford_Territory_004.jpg/1280px-Ford_Territory_004.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Ford_Territory_004.jpg/500px-Ford_Territory_004.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f8/Ford_Territory_003.jpg/1280px-Ford_Territory_003.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f8/Ford_Territory_003.jpg/500px-Ford_Territory_003.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/84/Ford_Territory_China_003.jpg/1280px-Ford_Territory_China_003.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/84/Ford_Territory_China_003.jpg/500px-Ford_Territory_China_003.jpg"),
        ]),
        ["Hyundai Creta"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/HYUNDAI_CRETA_%2C_iX25_%28SU2%29_China_%281%29.jpg/1280px-HYUNDAI_CRETA_%2C_iX25_%28SU2%29_China_%281%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/HYUNDAI_CRETA_%2C_iX25_%28SU2%29_China_%281%29.jpg/500px-HYUNDAI_CRETA_%2C_iX25_%28SU2%29_China_%281%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%289%29.jpg/1280px-HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%289%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%289%29.jpg/500px-HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%289%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/92/HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%2811%29.jpg/1280px-HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%2811%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/92/HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%2811%29.jpg/500px-HYUNDAI_CRETA_%2C_iX25_%28GS%2CGC%29_China_%2811%29.jpg"),
        ]),
        ["Hyundai HB20"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/6/6a/Red_hyundai_hb20.JPG",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6a/Red_hyundai_hb20.JPG/500px-Red_hyundai_hb20.JPG"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/2025_Hyundai_HB20_1.6_Comfort_Plus.jpg/1280px-2025_Hyundai_HB20_1.6_Comfort_Plus.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/2025_Hyundai_HB20_1.6_Comfort_Plus.jpg/500px-2025_Hyundai_HB20_1.6_Comfort_Plus.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Hyundai_HB20_1.6_Comfort_Plus_2025.jpg/1280px-Hyundai_HB20_1.6_Comfort_Plus_2025.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Hyundai_HB20_1.6_Comfort_Plus_2025.jpg/500px-Hyundai_HB20_1.6_Comfort_Plus_2025.jpg"),
        ]),
        ["Hyundai Santa Fe"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d4/2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5309.jpg/1280px-2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5309.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d4/2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5309.jpg/500px-2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5309.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5295.jpg/1280px-2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5295.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5295.jpg/500px-2024_Hyundai_Santa_Fe_%28MX5%29_IMG_5295.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/87/Hyundai_Santa_Fe%2C_GIMS_2018%2C_Le_Grand-Saconnex_%281X7A1735%29.jpg/1280px-Hyundai_Santa_Fe%2C_GIMS_2018%2C_Le_Grand-Saconnex_%281X7A1735%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/87/Hyundai_Santa_Fe%2C_GIMS_2018%2C_Le_Grand-Saconnex_%281X7A1735%29.jpg/500px-Hyundai_Santa_Fe%2C_GIMS_2018%2C_Le_Grand-Saconnex_%281X7A1735%29.jpg"),
        ]),
        ["Hyundai Tucson"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1858.jpg/1280px-Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1858.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1858.jpg/500px-Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1858.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f1/Hyundai_Tucson_%28NX4%29_1X7A0424.jpg/1280px-Hyundai_Tucson_%28NX4%29_1X7A0424.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f1/Hyundai_Tucson_%28NX4%29_1X7A0424.jpg/500px-Hyundai_Tucson_%28NX4%29_1X7A0424.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1859.jpg/1280px-Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1859.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1859.jpg/500px-Hyundai_Tucson_%28NX4%2C_SWB%29_PHEV_1X7A1859.jpg"),
        ]),
        ["Jeep Compass"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9c/Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0140.jpg/1280px-Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0140.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9c/Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0140.jpg/500px-Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0140.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/30/Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0139.jpg/1280px-Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0139.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/30/Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0139.jpg/500px-Jeep_Compass_%28MP%29_PHEV_Facelift_1X7A0139.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bb/JEEP_COMPASS_%28MK49%29_China_%283%29.jpg/1280px-JEEP_COMPASS_%28MK49%29_China_%283%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bb/JEEP_COMPASS_%28MK49%29_China_%283%29.jpg/500px-JEEP_COMPASS_%28MK49%29_China_%283%29.jpg"),
        ]),
        ["Jeep Renegade"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/88/Jeep_Renegade%2C_GIMS_2019%2C_Le_Grand-Saconnex_%28GIMS0538%29.jpg/1280px-Jeep_Renegade%2C_GIMS_2019%2C_Le_Grand-Saconnex_%28GIMS0538%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/88/Jeep_Renegade%2C_GIMS_2019%2C_Le_Grand-Saconnex_%28GIMS0538%29.jpg/500px-Jeep_Renegade%2C_GIMS_2019%2C_Le_Grand-Saconnex_%28GIMS0538%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/20/Jeep_Renegade_4xe_1X7A6025.jpg/1280px-Jeep_Renegade_4xe_1X7A6025.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/20/Jeep_Renegade_4xe_1X7A6025.jpg/500px-Jeep_Renegade_4xe_1X7A6025.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/ee/DSC00969-3_Unmarked_Jeep_Renegade%2C_Polizia_di_Stato%2C_Front_Left.jpg/1280px-DSC00969-3_Unmarked_Jeep_Renegade%2C_Polizia_di_Stato%2C_Front_Left.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ee/DSC00969-3_Unmarked_Jeep_Renegade%2C_Polizia_di_Stato%2C_Front_Left.jpg/500px-DSC00969-3_Unmarked_Jeep_Renegade%2C_Polizia_di_Stato%2C_Front_Left.jpg"),
        ]),
        ["Kia Picanto"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/2017_Kia_Picanto_GT-Line_S_1.2_Front.jpg/1280px-2017_Kia_Picanto_GT-Line_S_1.2_Front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/2017_Kia_Picanto_GT-Line_S_1.2_Front.jpg/500px-2017_Kia_Picanto_GT-Line_S_1.2_Front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2017_Kia_Picanto_GT-Line_S_1.2_Rear.jpg/1280px-2017_Kia_Picanto_GT-Line_S_1.2_Rear.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2017_Kia_Picanto_GT-Line_S_1.2_Rear.jpg/500px-2017_Kia_Picanto_GT-Line_S_1.2_Rear.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/Kia_Picanto_%28SA%29_DSC_7987.jpg/1280px-Kia_Picanto_%28SA%29_DSC_7987.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/54/Kia_Picanto_%28SA%29_DSC_7987.jpg/500px-Kia_Picanto_%28SA%29_DSC_7987.jpg"),
        ]),
        ["Kia Rio"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/2017_Kia_Rio_2_1.3_Front.jpg/1280px-2017_Kia_Rio_2_1.3_Front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/2017_Kia_Rio_2_1.3_Front.jpg/500px-2017_Kia_Rio_2_1.3_Front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/cc/2016_Kia_Rio_Red_Line_white_rear.jpg/1280px-2016_Kia_Rio_Red_Line_white_rear.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cc/2016_Kia_Rio_Red_Line_white_rear.jpg/500px-2016_Kia_Rio_Red_Line_white_rear.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f7/Kia_Rio_%28UB%29_Washington_DC_Metro_Area%2C_USA.jpg/1280px-Kia_Rio_%28UB%29_Washington_DC_Metro_Area%2C_USA.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f7/Kia_Rio_%28UB%29_Washington_DC_Metro_Area%2C_USA.jpg/500px-Kia_Rio_%28UB%29_Washington_DC_Metro_Area%2C_USA.jpg"),
        ]),
        ["Kia Sorento"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2024_Kia_Sorento_%28MQ4%29_Ditzingen_Mobil_IMG_9803.jpg/1280px-2024_Kia_Sorento_%28MQ4%29_Ditzingen_Mobil_IMG_9803.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2024_Kia_Sorento_%28MQ4%29_Ditzingen_Mobil_IMG_9803.jpg/500px-2024_Kia_Sorento_%28MQ4%29_Ditzingen_Mobil_IMG_9803.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/KIA_SORENTO_%28BL%29_China.jpg/1280px-KIA_SORENTO_%28BL%29_China.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/KIA_SORENTO_%28BL%29_China.jpg/500px-KIA_SORENTO_%28BL%29_China.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/00_KIA_SORENTO_HEV_1.jpg/1280px-00_KIA_SORENTO_HEV_1.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/00_KIA_SORENTO_HEV_1.jpg/500px-00_KIA_SORENTO_HEV_1.jpg"),
        ]),
        ["Kia Sportage"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_1X7A0317.jpg/1280px-Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_1X7A0317.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_1X7A0317.jpg/500px-Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_1X7A0317.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bf/Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_IAA_2021_1X7A0113.jpg/1280px-Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_IAA_2021_1X7A0113.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bf/Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_IAA_2021_1X7A0113.jpg/500px-Kia_Sportage_Plug-in-Hybrid_%28NQ5%29_IAA_2021_1X7A0113.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Kia_Sportage_%28NQ5%29_1X7A0319.jpg/1280px-Kia_Sportage_%28NQ5%29_1X7A0319.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Kia_Sportage_%28NQ5%29_1X7A0319.jpg/500px-Kia_Sportage_%28NQ5%29_1X7A0319.jpg"),
        ]),
        ["Nissan Frontier"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/3e/Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5885.jpg/1280px-Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5885.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3e/Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5885.jpg/500px-Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5885.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/1d/Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5942.jpg/1280px-Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5942.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1d/Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5942.jpg/500px-Nissan_Frontier_%28D41%29_Pro-4X_Automesse_Ludwigsburg_2022_1X7A5942.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bc/2016_Nissan_NP300_Navara_Tekna_DCi_3.0.jpg/1280px-2016_Nissan_NP300_Navara_Tekna_DCi_3.0.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bc/2016_Nissan_NP300_Navara_Tekna_DCi_3.0.jpg/500px-2016_Nissan_NP300_Navara_Tekna_DCi_3.0.jpg"),
        ]),
        ["Nissan Kicks"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/07/Nissan_Kicks_%28P16%29_DSC_2875.jpg/1280px-Nissan_Kicks_%28P16%29_DSC_2875.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/07/Nissan_Kicks_%28P16%29_DSC_2875.jpg/500px-Nissan_Kicks_%28P16%29_DSC_2875.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/NISSAN_KICKS_China.jpg/1280px-NISSAN_KICKS_China.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/NISSAN_KICKS_China.jpg/500px-NISSAN_KICKS_China.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b8/NISSAN_KICKS_China_%287%29.jpg/1280px-NISSAN_KICKS_China_%287%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b8/NISSAN_KICKS_China_%287%29.jpg/500px-NISSAN_KICKS_China_%287%29.jpg"),
        ]),
        ["Nissan March"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/7d/Nissan_Micra_%28K14%29_DSC_7790_%28cropped%29.jpg/1280px-Nissan_Micra_%28K14%29_DSC_7790_%28cropped%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7d/Nissan_Micra_%28K14%29_DSC_7790_%28cropped%29.jpg/500px-Nissan_Micra_%28K14%29_DSC_7790_%28cropped%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/80/Nissan_Micra_%28K14%29_DSC_7788_%28cropped%29.jpg/1280px-Nissan_Micra_%28K14%29_DSC_7788_%28cropped%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/80/Nissan_Micra_%28K14%29_DSC_7788_%28cropped%29.jpg/500px-Nissan_Micra_%28K14%29_DSC_7788_%28cropped%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5d/Nissan_Micra_EV_IMG_4978.jpg/1280px-Nissan_Micra_EV_IMG_4978.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5d/Nissan_Micra_EV_IMG_4978.jpg/500px-Nissan_Micra_EV_IMG_4978.jpg"),
        ]),
        ["Nissan Versa"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/2b/Versa2021p1.jpg/1280px-Versa2021p1.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2b/Versa2021p1.jpg/500px-Versa2021p1.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/Versa2021p2.jpg/1280px-Versa2021p2.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/Versa2021p2.jpg/500px-Versa2021p2.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/2023_Nissan_Versa_%28N18%29_DSC_2669.jpg/1280px-2023_Nissan_Versa_%28N18%29_DSC_2669.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/2023_Nissan_Versa_%28N18%29_DSC_2669.jpg/500px-2023_Nissan_Versa_%28N18%29_DSC_2669.jpg"),
        ]),
        ["Peugeot 2008"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/43/Peugeot_e-2008_Automesse_Ludwigsburg_2022_1X7A5883.jpg/1280px-Peugeot_e-2008_Automesse_Ludwigsburg_2022_1X7A5883.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/43/Peugeot_e-2008_Automesse_Ludwigsburg_2022_1X7A5883.jpg/500px-Peugeot_e-2008_Automesse_Ludwigsburg_2022_1X7A5883.jpg"),
            new("https://commons.wikimedia.org/wiki/Special:FilePath/%D0%90%D1%81%D0%B2%D0%B5%D1%81%D1%82%D0%BE%D1%85%D0%BE%D1%80%D0%B8%2C_Peugeot_2008_%D0%BD%D0%B0_25_%D0%9C%D0%B0%D1%80%D1%82%D0%B0-%D0%9C%D0%B0%D0%BA%D0%B5%D0%B4%D0%BE%D0%BD%D0%BE%D0%BC%D0%B0%D1%85%D0%BE%D0%BD%D0%B5.jpg?width=1280",
                "https://commons.wikimedia.org/wiki/Special:FilePath/%D0%90%D1%81%D0%B2%D0%B5%D1%81%D1%82%D0%BE%D1%85%D0%BE%D1%80%D0%B8%2C_Peugeot_2008_%D0%BD%D0%B0_25_%D0%9C%D0%B0%D1%80%D1%82%D0%B0-%D0%9C%D0%B0%D0%BA%D0%B5%D0%B4%D0%BE%D0%BD%D0%BE%D0%BC%D0%B0%D1%85%D0%BE%D0%BD%D0%B5.jpg?width=500"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b0/Peugeot_e-2008_Facelift_Autofr%C3%BChling_Ulm_IMG_9288.jpg/1280px-Peugeot_e-2008_Facelift_Autofr%C3%BChling_Ulm_IMG_9288.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b0/Peugeot_e-2008_Facelift_Autofr%C3%BChling_Ulm_IMG_9288.jpg/500px-Peugeot_e-2008_Facelift_Autofr%C3%BChling_Ulm_IMG_9288.jpg"),
        ]),
        ["Peugeot 208"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/ca/2020_Peugeot_208_Active.jpg/1280px-2020_Peugeot_208_Active.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/ca/2020_Peugeot_208_Active.jpg/500px-2020_Peugeot_208_Active.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5b/2020_Peugeot_208_Allure.jpg/1280px-2020_Peugeot_208_Allure.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5b/2020_Peugeot_208_Allure.jpg/500px-2020_Peugeot_208_Allure.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/14/2020_Peugeot_e-208_GT.jpg/1280px-2020_Peugeot_e-208_GT.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/14/2020_Peugeot_e-208_GT.jpg/500px-2020_Peugeot_e-208_GT.jpg"),
        ]),
        ["Peugeot 3008"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/ed/Peugeot_e-3008_Automesse_Ludwigsburg_2024_IMG_1537.jpg/1280px-Peugeot_e-3008_Automesse_Ludwigsburg_2024_IMG_1537.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ed/Peugeot_e-3008_Automesse_Ludwigsburg_2024_IMG_1537.jpg/500px-Peugeot_e-3008_Automesse_Ludwigsburg_2024_IMG_1537.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/Peugeot_3008_C_DSC_8318.jpg/1280px-Peugeot_3008_C_DSC_8318.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/Peugeot_3008_C_DSC_8318.jpg/500px-Peugeot_3008_C_DSC_8318.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2021_Peugeot_3008_B_Hybrid4_1X7A5797.jpg/1280px-2021_Peugeot_3008_B_Hybrid4_1X7A5797.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/2021_Peugeot_3008_B_Hybrid4_1X7A5797.jpg/500px-2021_Peugeot_3008_B_Hybrid4_1X7A5797.jpg"),
        ]),
        ["Peugeot Landtrek"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/98/2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view.jpg/1280px-2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/98/2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view.jpg/500px-2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/96/Peugeot_Landtrek_01.jpg/1280px-Peugeot_Landtrek_01.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/96/Peugeot_Landtrek_01.jpg/500px-Peugeot_Landtrek_01.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4a/2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view_02.jpg/1280px-2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view_02.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4a/2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view_02.jpg/500px-2022_Peugeot_Landtrek_1.9_HDi_Active_%28Chile%29_front_view_02.jpg"),
        ]),
        ["Peugeot Partner"] = new(Carroceria.Van, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/ae/Citro%C3%ABn_Berlingo_III_IMG_9227.jpg/1280px-Citro%C3%ABn_Berlingo_III_IMG_9227.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ae/Citro%C3%ABn_Berlingo_III_IMG_9227.jpg/500px-Citro%C3%ABn_Berlingo_III_IMG_9227.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/33/Peugeot_Partner_PN_des_Pyr%C3%A9n%C3%A9es_Bar%C3%A8ges_2012.jpg/1280px-Peugeot_Partner_PN_des_Pyr%C3%A9n%C3%A9es_Bar%C3%A8ges_2012.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/33/Peugeot_Partner_PN_des_Pyr%C3%A9n%C3%A9es_Bar%C3%A8ges_2012.jpg/500px-Peugeot_Partner_PN_des_Pyr%C3%A9n%C3%A9es_Bar%C3%A8ges_2012.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Peugeot_e-Partner_Auto_Zuerich_2023_1X7A1428.jpg/1280px-Peugeot_e-Partner_Auto_Zuerich_2023_1X7A1428.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Peugeot_e-Partner_Auto_Zuerich_2023_1X7A1428.jpg/500px-Peugeot_e-Partner_Auto_Zuerich_2023_1X7A1428.jpg"),
        ]),
        ["Renault Duster"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/49/Dacia_Duster_III_IMG_8970.jpg/1280px-Dacia_Duster_III_IMG_8970.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/49/Dacia_Duster_III_IMG_8970.jpg/500px-Dacia_Duster_III_IMG_8970.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9b/Dacia_Duster_III_IMG_8961.jpg/1280px-Dacia_Duster_III_IMG_8961.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9b/Dacia_Duster_III_IMG_8961.jpg/500px-Dacia_Duster_III_IMG_8961.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Dacia_Duster_III_IMG_8973.jpg/1280px-Dacia_Duster_III_IMG_8973.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Dacia_Duster_III_IMG_8973.jpg/500px-Dacia_Duster_III_IMG_8973.jpg"),
        ]),
        ["Renault Kangoo"] = new(Carroceria.Van, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/2a/Renault_Kangoo_III_1X7A1519.jpg/1280px-Renault_Kangoo_III_1X7A1519.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2a/Renault_Kangoo_III_1X7A1519.jpg/500px-Renault_Kangoo_III_1X7A1519.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Nissan_NV250_IMG_2192.jpg/1280px-Nissan_NV250_IMG_2192.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Nissan_NV250_IMG_2192.jpg/500px-Nissan_NV250_IMG_2192.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Renault_Kangoo_III_1X7A1518.jpg/1280px-Renault_Kangoo_III_1X7A1518.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Renault_Kangoo_III_1X7A1518.jpg/500px-Renault_Kangoo_III_1X7A1518.jpg"),
        ]),
        ["Renault Kwid"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/33/Renault_Kwid_2017_in_Montevideo_%28front%29.jpg/1280px-Renault_Kwid_2017_in_Montevideo_%28front%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/33/Renault_Kwid_2017_in_Montevideo_%28front%29.jpg/500px-Renault_Kwid_2017_in_Montevideo_%28front%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Renault_KWID_RXT%28O%29.jpg/1280px-Renault_KWID_RXT%28O%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Renault_KWID_RXT%28O%29.jpg/500px-Renault_KWID_RXT%28O%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Renault_Kwid_%2853343921268%29.jpg/1280px-Renault_Kwid_%2853343921268%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/53/Renault_Kwid_%2853343921268%29.jpg/500px-Renault_Kwid_%2853343921268%29.jpg"),
        ]),
        ["Renault Logan"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d5/2023_Dacia_Logan_III_IMG_9678.jpg/1280px-2023_Dacia_Logan_III_IMG_9678.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d5/2023_Dacia_Logan_III_IMG_9678.jpg/500px-2023_Dacia_Logan_III_IMG_9678.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/17/2023_Dacia_Logan_III_IMG_9671.jpg/1280px-2023_Dacia_Logan_III_IMG_9671.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/17/2023_Dacia_Logan_III_IMG_9671.jpg/500px-2023_Dacia_Logan_III_IMG_9671.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0a/2023_Dacia_Logan_III_IMG_9671_%28cropped%29.jpg/1280px-2023_Dacia_Logan_III_IMG_9671_%28cropped%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0a/2023_Dacia_Logan_III_IMG_9671_%28cropped%29.jpg/500px-2023_Dacia_Logan_III_IMG_9671_%28cropped%29.jpg"),
        ]),
        ["Renault Oroch"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/3d/Renault_Duster_Oroch_2016_in_Punta_del_Este_01.JPG/1280px-Renault_Duster_Oroch_2016_in_Punta_del_Este_01.JPG",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3d/Renault_Duster_Oroch_2016_in_Punta_del_Este_01.JPG/500px-Renault_Duster_Oroch_2016_in_Punta_del_Este_01.JPG"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Renault_Oroch%2C_Bariloche.jpg/1280px-Renault_Oroch%2C_Bariloche.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Renault_Oroch%2C_Bariloche.jpg/500px-Renault_Oroch%2C_Bariloche.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b6/Renault_Duster_Oroch_2.0_Intens_2018_%2832431308657%29.jpg/1280px-Renault_Duster_Oroch_2.0_Intens_2018_%2832431308657%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b6/Renault_Duster_Oroch_2.0_Intens_2018_%2832431308657%29.jpg/500px-Renault_Duster_Oroch_2.0_Intens_2018_%2832431308657%29.jpg"),
        ]),
        ["Renault Sandero"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/db/Dacia_Sandero_III_1X7A0329.jpg/1280px-Dacia_Sandero_III_1X7A0329.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/db/Dacia_Sandero_III_1X7A0329.jpg/500px-Dacia_Sandero_III_1X7A0329.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/Dacia_Sandero_III_1X7A6451.jpg/1280px-Dacia_Sandero_III_1X7A6451.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/59/Dacia_Sandero_III_1X7A6451.jpg/500px-Dacia_Sandero_III_1X7A6451.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/1b/Dacia_Sandero_III_1X7A0382.jpg/1280px-Dacia_Sandero_III_1X7A0382.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1b/Dacia_Sandero_III_1X7A0382.jpg/500px-Dacia_Sandero_III_1X7A0382.jpg"),
        ]),
        ["Suzuki Baleno"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/23/2019_Suzuki_Baleno_GL_1.4_WB52S_%2820211006%29.jpg/1280px-2019_Suzuki_Baleno_GL_1.4_WB52S_%2820211006%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/23/2019_Suzuki_Baleno_GL_1.4_WB52S_%2820211006%29.jpg/500px-2019_Suzuki_Baleno_GL_1.4_WB52S_%2820211006%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/2019_Suzuki_Baleno_GL_1.4_WB52S_%2820210313%29.jpg/1280px-2019_Suzuki_Baleno_GL_1.4_WB52S_%2820210313%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/2019_Suzuki_Baleno_GL_1.4_WB52S_%2820210313%29.jpg/500px-2019_Suzuki_Baleno_GL_1.4_WB52S_%2820210313%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/2019_Suzuki_Baleno_1.4_%28Indonesia%29_front_view.jpg/1280px-2019_Suzuki_Baleno_1.4_%28Indonesia%29_front_view.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/2019_Suzuki_Baleno_1.4_%28Indonesia%29_front_view.jpg/500px-2019_Suzuki_Baleno_1.4_%28Indonesia%29_front_view.jpg"),
        ]),
        ["Suzuki Jimny"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e8/Suzuki_Jimny%2C_Iceland%2C_20230430_1704_3701.jpg/1280px-Suzuki_Jimny%2C_Iceland%2C_20230430_1704_3701.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e8/Suzuki_Jimny%2C_Iceland%2C_20230430_1704_3701.jpg/500px-Suzuki_Jimny%2C_Iceland%2C_20230430_1704_3701.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/06/Suzuki_Jimny%2C_Kleifarvatn%2C_Iceland%2C_20230503_0841_4296.jpg/1280px-Suzuki_Jimny%2C_Kleifarvatn%2C_Iceland%2C_20230503_0841_4296.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/06/Suzuki_Jimny%2C_Kleifarvatn%2C_Iceland%2C_20230503_0841_4296.jpg/500px-Suzuki_Jimny%2C_Kleifarvatn%2C_Iceland%2C_20230503_0841_4296.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Suzuki_Jimny_on_the_parking_near_%C3%9Eorbj%C3%B6rn_Mountain%2C_Iceland%2C_20230430_1630_3697.jpg/1280px-Suzuki_Jimny_on_the_parking_near_%C3%9Eorbj%C3%B6rn_Mountain%2C_Iceland%2C_20230430_1630_3697.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c7/Suzuki_Jimny_on_the_parking_near_%C3%9Eorbj%C3%B6rn_Mountain%2C_Iceland%2C_20230430_1630_3697.jpg/500px-Suzuki_Jimny_on_the_parking_near_%C3%9Eorbj%C3%B6rn_Mountain%2C_Iceland%2C_20230430_1630_3697.jpg"),
        ]),
        ["Suzuki Swift"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/2018_Suzuki_Swift_SZ5_Boosterjet_SHVS_1.0_Front.jpg/1280px-2018_Suzuki_Swift_SZ5_Boosterjet_SHVS_1.0_Front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/2018_Suzuki_Swift_SZ5_Boosterjet_SHVS_1.0_Front.jpg/500px-2018_Suzuki_Swift_SZ5_Boosterjet_SHVS_1.0_Front.jpg"),
            new("https://commons.wikimedia.org/wiki/Special:FilePath/%D0%90%D1%81%D0%B2%D0%B5%D1%81%D1%82%D0%BE%D1%85%D0%BE%D1%80%D0%B8%2C_Suzuki_Swift_%D0%BD%D0%B0_25_%D0%9C%D0%B0%D1%80%D1%82%D0%B0-%D0%9C%D0%B0%D0%BA%D0%B5%D0%B4%D0%BE%D0%BD%D0%BE%D0%BC%D0%B0%D1%85%D0%BE%D0%BD%D0%B5.jpg?width=1280",
                "https://commons.wikimedia.org/wiki/Special:FilePath/%D0%90%D1%81%D0%B2%D0%B5%D1%81%D1%82%D0%BE%D1%85%D0%BE%D1%80%D0%B8%2C_Suzuki_Swift_%D0%BD%D0%B0_25_%D0%9C%D0%B0%D1%80%D1%82%D0%B0-%D0%9C%D0%B0%D0%BA%D0%B5%D0%B4%D0%BE%D0%BD%D0%BE%D0%BC%D0%B0%D1%85%D0%BE%D0%BD%D0%B5.jpg?width=500"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/30/Suzuki_Swift_%282024%29_hybrid_DSC_7922.jpg/1280px-Suzuki_Swift_%282024%29_hybrid_DSC_7922.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/30/Suzuki_Swift_%282024%29_hybrid_DSC_7922.jpg/500px-Suzuki_Swift_%282024%29_hybrid_DSC_7922.jpg"),
        ]),
        ["Suzuki Vitara"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/a2/Suzuki_e_Vitara_Z_4WD.jpg/1280px-Suzuki_e_Vitara_Z_4WD.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a2/Suzuki_e_Vitara_Z_4WD.jpg/500px-Suzuki_e_Vitara_Z_4WD.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/7/7b/2024_Suzuki_Vitara_%284th_generation%29_DSC_6083.jpg/1280px-2024_Suzuki_Vitara_%284th_generation%29_DSC_6083.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7b/2024_Suzuki_Vitara_%284th_generation%29_DSC_6083.jpg/500px-2024_Suzuki_Vitara_%284th_generation%29_DSC_6083.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/2024_Suzuki_Vitara_%284th_generation%29_DSC_7925.jpg/1280px-2024_Suzuki_Vitara_%284th_generation%29_DSC_7925.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/2024_Suzuki_Vitara_%284th_generation%29_DSC_7925.jpg/500px-2024_Suzuki_Vitara_%284th_generation%29_DSC_7925.jpg"),
        ]),
        ["Toyota Corolla"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/43/2020_Toyota_Corolla_LE_sedan.jpg/1280px-2020_Toyota_Corolla_LE_sedan.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/43/2020_Toyota_Corolla_LE_sedan.jpg/500px-2020_Toyota_Corolla_LE_sedan.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2020_Toyota_Corolla_LE_12-13-2019.jpg/1280px-2020_Toyota_Corolla_LE_12-13-2019.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/2020_Toyota_Corolla_LE_12-13-2019.jpg/500px-2020_Toyota_Corolla_LE_12-13-2019.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/2020_Toyota_Corolla_SE_2.0L_sedan%2C_12.28.19.jpg/1280px-2020_Toyota_Corolla_SE_2.0L_sedan%2C_12.28.19.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/2020_Toyota_Corolla_SE_2.0L_sedan%2C_12.28.19.jpg/500px-2020_Toyota_Corolla_SE_2.0L_sedan%2C_12.28.19.jpg"),
        ]),
        ["Toyota Corolla Cross"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0b/Toyota_Corolla_Cross_Hybrid_1X7A1861.jpg/1280px-Toyota_Corolla_Cross_Hybrid_1X7A1861.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0b/Toyota_Corolla_Cross_Hybrid_1X7A1861.jpg/500px-Toyota_Corolla_Cross_Hybrid_1X7A1861.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/05/Toyota_Corolla_Cross_Hybrid_1X7A1862.jpg/1280px-Toyota_Corolla_Cross_Hybrid_1X7A1862.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/05/Toyota_Corolla_Cross_Hybrid_1X7A1862.jpg/500px-Toyota_Corolla_Cross_Hybrid_1X7A1862.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/2022_Toyota_Corolla_Cross_L_FWD%2C_Front_Left%2C_11-21-2021.jpg/1280px-2022_Toyota_Corolla_Cross_L_FWD%2C_Front_Left%2C_11-21-2021.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/2022_Toyota_Corolla_Cross_L_FWD%2C_Front_Left%2C_11-21-2021.jpg/500px-2022_Toyota_Corolla_Cross_L_FWD%2C_Front_Left%2C_11-21-2021.jpg"),
        ]),
        ["Toyota Hilux"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Toyota_HiLux_GR_Sport_1X7A7281.jpg/1280px-Toyota_HiLux_GR_Sport_1X7A7281.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Toyota_HiLux_GR_Sport_1X7A7281.jpg/500px-Toyota_HiLux_GR_Sport_1X7A7281.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/08/2026_Toyota_Hilux_Z.jpg/1280px-2026_Toyota_Hilux_Z.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/08/2026_Toyota_Hilux_Z.jpg/500px-2026_Toyota_Hilux_Z.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5a/2026_Toyota_Hilux_BEV.jpg/1280px-2026_Toyota_Hilux_BEV.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5a/2026_Toyota_Hilux_BEV.jpg/500px-2026_Toyota_Hilux_BEV.jpg"),
        ]),
        ["Toyota RAV4"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/5/5b/2021_Toyota_RAV4_PHV.jpg/1280px-2021_Toyota_RAV4_PHV.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5b/2021_Toyota_RAV4_PHV.jpg/500px-2021_Toyota_RAV4_PHV.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/2021_Giro_d%27Italia_official_car_%28Toyota_RAV4%29.jpg/1280px-2021_Giro_d%27Italia_official_car_%28Toyota_RAV4%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/2021_Giro_d%27Italia_official_car_%28Toyota_RAV4%29.jpg/500px-2021_Giro_d%27Italia_official_car_%28Toyota_RAV4%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/2021_Toyota_RAV4_XLE_AWD%2C_front_right%2C_05-24-2026.jpg/1280px-2021_Toyota_RAV4_XLE_AWD%2C_front_right%2C_05-24-2026.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/2021_Toyota_RAV4_XLE_AWD%2C_front_right%2C_05-24-2026.jpg/500px-2021_Toyota_RAV4_XLE_AWD%2C_front_right%2C_05-24-2026.jpg"),
        ]),
        ["Toyota Yaris"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Toyota_Vitz%2C_Korle-Klottey_%28P1100243%29.jpg/1280px-Toyota_Vitz%2C_Korle-Klottey_%28P1100243%29.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Toyota_Vitz%2C_Korle-Klottey_%28P1100243%29.jpg/500px-Toyota_Vitz%2C_Korle-Klottey_%28P1100243%29.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/4e/%D0%9B%D0%B0%D0%B3%D0%BA%D0%B0%D0%B4%D0%B8%D0%BA%D0%B8%D1%8F%2C_Toyota_Yaris.jpg/1280px-%D0%9B%D0%B0%D0%B3%D0%BA%D0%B0%D0%B4%D0%B8%D0%BA%D0%B8%D1%8F%2C_Toyota_Yaris.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4e/%D0%9B%D0%B0%D0%B3%D0%BA%D0%B0%D0%B4%D0%B8%D0%BA%D0%B8%D1%8F%2C_Toyota_Yaris.jpg/500px-%D0%9B%D0%B0%D0%B3%D0%BA%D0%B0%D0%B4%D0%B8%D0%BA%D0%B8%D1%8F%2C_Toyota_Yaris.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/Toyota_Yaris_GRMN_IMG_0856.jpg/1280px-Toyota_Yaris_GRMN_IMG_0856.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/Toyota_Yaris_GRMN_IMG_0856.jpg/500px-Toyota_Yaris_GRMN_IMG_0856.jpg"),
        ]),
        ["Volkswagen Amarok"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/Volkswagen_Amarok_Mk2_1X7A0852.jpg/1280px-Volkswagen_Amarok_Mk2_1X7A0852.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/Volkswagen_Amarok_Mk2_1X7A0852.jpg/500px-Volkswagen_Amarok_Mk2_1X7A0852.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/9/97/Volkswagen_Amarok_Mk2_1X7A0807.jpg/1280px-Volkswagen_Amarok_Mk2_1X7A0807.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/9/97/Volkswagen_Amarok_Mk2_1X7A0807.jpg/500px-Volkswagen_Amarok_Mk2_1X7A0807.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/a8/Volkswagen_Amarok_Mk2_1X7A0808.jpg/1280px-Volkswagen_Amarok_Mk2_1X7A0808.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a8/Volkswagen_Amarok_Mk2_1X7A0808.jpg/500px-Volkswagen_Amarok_Mk2_1X7A0808.jpg"),
        ]),
        ["Volkswagen Gol"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e4/Volkswagen_Gol_sed%C3%A1n_Mk6_in_Uruguay.jpg/1280px-Volkswagen_Gol_sed%C3%A1n_Mk6_in_Uruguay.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e4/Volkswagen_Gol_sed%C3%A1n_Mk6_in_Uruguay.jpg/500px-Volkswagen_Gol_sed%C3%A1n_Mk6_in_Uruguay.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_front.jpg/1280px-Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_front.jpg/500px-Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/a/a3/Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_rear.jpg/1280px-Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_rear.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a3/Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_rear.jpg/500px-Volkswagen_Gol_MkI_red_and_black_in_Uruguay_-_rear.jpg"),
        ]),
        ["Volkswagen Polo"] = new(Carroceria.Hatchback, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/2019_Volkswagen_Polo_1.6_MSi_Highline.jpg/1280px-2019_Volkswagen_Polo_1.6_MSi_Highline.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/2019_Volkswagen_Polo_1.6_MSi_Highline.jpg/500px-2019_Volkswagen_Polo_1.6_MSi_Highline.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_1.jpg/1280px-2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_1.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_1.jpg/500px-2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_1.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_2.jpg/1280px-2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_2.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_2.jpg/500px-2019_Volkswagen_Polo_1.6_MSi_Comfortline_-_2.jpg"),
        ]),
        ["Volkswagen Saveiro"] = new(Carroceria.Pickup, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/d/d6/Volkswagen_Saveiro_NF_2010_-_front.jpg/1280px-Volkswagen_Saveiro_NF_2010_-_front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d6/Volkswagen_Saveiro_NF_2010_-_front.jpg/500px-Volkswagen_Saveiro_NF_2010_-_front.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/2/26/Volkswagen_Gol_1.6.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/26/Volkswagen_Gol_1.6.jpg/500px-Volkswagen_Gol_1.6.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/1/15/VW_Gol_2009_front.jpg/1280px-VW_Gol_2009_front.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/15/VW_Gol_2009_front.jpg/500px-VW_Gol_2009_front.jpg"),
        ]),
        ["Volkswagen T-Cross"] = new(Carroceria.Suv, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f5/Volkswagen_T-Cross_1X7A0366.jpg/1280px-Volkswagen_T-Cross_1X7A0366.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f5/Volkswagen_T-Cross_1X7A0366.jpg/500px-Volkswagen_T-Cross_1X7A0366.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/eb/Volkswagen_T-Cross_1X7A0363.jpg/1280px-Volkswagen_T-Cross_1X7A0363.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/eb/Volkswagen_T-Cross_1X7A0363.jpg/500px-Volkswagen_T-Cross_1X7A0363.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Volkswagen_T-Cross_1X7A0364.jpg/1280px-Volkswagen_T-Cross_1X7A0364.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/2/28/Volkswagen_T-Cross_1X7A0364.jpg/500px-Volkswagen_T-Cross_1X7A0364.jpg"),
        ]),
        ["Volkswagen Virtus"] = new(Carroceria.Sedan, [
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/ef/2023_Volkswagen_Virtus_Topline_front_20230520.jpg/1280px-2023_Volkswagen_Virtus_Topline_front_20230520.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ef/2023_Volkswagen_Virtus_Topline_front_20230520.jpg/500px-2023_Volkswagen_Virtus_Topline_front_20230520.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/f/fa/2023_Volkswagen_Virtus_Topline_rear_20230520.jpg/1280px-2023_Volkswagen_Virtus_Topline_rear_20230520.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/fa/2023_Volkswagen_Virtus_Topline_rear_20230520.jpg/500px-2023_Volkswagen_Virtus_Topline_rear_20230520.jpg"),
            new("https://upload.wikimedia.org/wikipedia/commons/thumb/e/e3/2018_Volkswagen_Virtus_1.6_MSi_Highline_AT.jpg/1280px-2018_Volkswagen_Virtus_1.6_MSi_Highline_AT.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e3/2018_Volkswagen_Virtus_1.6_MSi_Highline_AT.jpg/500px-2018_Volkswagen_Virtus_1.6_MSi_Highline_AT.jpg"),
        ]),
    };
}
