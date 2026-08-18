/**
 * Achica una foto en el navegador antes de subirla.
 *
 * Es lo que hace cumplible el criterio de aceptación de cargar diez fotos desde el celular
 * sin timeout: una foto de un teléfono actual pesa entre 3 y 8 MB, y diez de esas por 4G
 * son varios minutos de subida y un buen riesgo de que se corte. Redimensionadas a 1600 px
 * quedan en torno a los 200 KB, que es más resolución de la que cualquier galería web
 * llega a mostrar.
 *
 * También saca los metadatos EXIF de yapa, incluida la geolocalización: la foto del auto
 * no tiene por qué publicar dónde vive el dueño.
 */
export const LADO_MAXIMO = 1600
export const CALIDAD = 0.82

export async function achicar(
  archivo: File,
  ladoMaximo = LADO_MAXIMO,
  calidad = CALIDAD,
): Promise<Blob> {
  const bitmap = await createImageBitmap(archivo)

  try {
    const escala = Math.min(1, ladoMaximo / Math.max(bitmap.width, bitmap.height))

    // Si ya es chica no se re-comprime: volver a codificar un JPEG siempre pierde calidad.
    if (escala === 1 && archivo.size <= 900_000) {
      return archivo
    }

    const ancho = Math.round(bitmap.width * escala)
    const alto = Math.round(bitmap.height * escala)

    const lienzo = document.createElement('canvas')
    lienzo.width = ancho
    lienzo.height = alto

    const contexto = lienzo.getContext('2d')

    if (!contexto) {
      // Sin canvas se sube el original: es peor que tarde a que no se pueda cargar nada.
      return archivo
    }

    contexto.drawImage(bitmap, 0, 0, ancho, alto)

    const comprimida = await new Promise<Blob | null>((resolver) =>
      lienzo.toBlob(resolver, 'image/jpeg', calidad),
    )

    return comprimida ?? archivo
  } finally {
    bitmap.close()
  }
}

/** Nombre de archivo con extensión coherente con lo que realmente se sube. */
export function nombreDeSubida(archivo: File, subida: Blob): string {
  if (subida === archivo) return archivo.name

  const sinExtension = archivo.name.replace(/\.[^.]+$/, '')
  return `${sinExtension || 'foto'}.jpg`
}
