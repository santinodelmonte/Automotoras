/**
 * Colores derivados del color de marca de cada automotora.
 *
 * El color lo elige cada cliente y puede ser cualquiera. Un amarillo de marca usado como
 * texto sobre blanco no se lee, y un celeste con letras blancas tampoco. Lo legible se
 * calcula acá, una sola vez, en vez de depender de que cada automotora elija bien.
 */

type Rgb = [number, number, number]

const BLANCO: Rgb = [255, 255, 255]
const TINTA: Rgb = [11, 18, 32]

export interface ColoresDeMarca {
  /** El color tal cual, para fondos y acentos. */
  marca: string
  /** Texto sobre el color de marca: blanco mientras se lea, tinta si el color es claro. */
  contraste: string
  /** El color de marca oscurecido lo justo para leerse como texto sobre blanco. */
  tinta: string
}

export function coloresDeMarca(hex: string): ColoresDeMarca {
  const rgb = aRgb(hex) ?? TINTA

  // Blanco hasta 3:1, que es lo que WCAG pide para texto grande y para controles: los
  // botones y badges de marca son eso. Las marcas claras —amarillos, celestes— pasan a
  // tinta, que es lo que haría cualquier diseñador con esos colores.
  const contraste = contrasteEntre(rgb, BLANCO) >= 3 ? '#ffffff' : aHex(TINTA)

  // Para texto chico sobre blanco hace falta 4.5:1. Se oscurece de a poco hacia la tinta
  // hasta llegar; un color que ya cumple no se toca.
  let tinta = rgb
  for (let paso = 1; contrasteEntre(tinta, BLANCO) < 4.5 && paso <= 10; paso++) {
    tinta = rgb.map((canal, i) => canal + (TINTA[i] - canal) * (paso / 10)) as Rgb
  }

  return { marca: aHex(rgb), contraste, tinta: aHex(tinta) }
}

function aRgb(hex: string): Rgb | null {
  const limpio = hex.trim().replace(/^#/, '')
  const completo =
    limpio.length === 3
      ? limpio
          .split('')
          .map((c) => c + c)
          .join('')
      : limpio

  if (!/^[0-9a-f]{6}$/i.test(completo)) return null

  return [0, 2, 4].map((i) => parseInt(completo.slice(i, i + 2), 16)) as Rgb
}

function aHex(rgb: Rgb): string {
  return `#${rgb.map((canal) => Math.round(canal).toString(16).padStart(2, '0')).join('')}`
}

/** Luminancia relativa, como la define WCAG 2. */
function luminancia(rgb: Rgb): number {
  const [r, g, b] = rgb.map((canal) => {
    const s = canal / 255
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4
  })

  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

function contrasteEntre(a: Rgb, b: Rgb): number {
  const [clara, oscura] = [luminancia(a), luminancia(b)].sort((x, y) => y - x)
  return (clara + 0.05) / (oscura + 0.05)
}
