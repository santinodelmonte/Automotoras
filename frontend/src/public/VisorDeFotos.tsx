import Lightbox from 'yet-another-react-lightbox'
import Counter from 'yet-another-react-lightbox/plugins/counter'
import Zoom from 'yet-another-react-lightbox/plugins/zoom'
import 'yet-another-react-lightbox/styles.css'
import 'yet-another-react-lightbox/plugins/counter.css'
import type { VehiculoFoto } from '@shared/api/types'

interface Props {
  fotos: VehiculoFoto[]
  indice: number
  titulo: string
  onCerrar: () => void
}

/**
 * Las fotos a pantalla completa, con zoom: pellizcando en el celular, con la rueda o con
 * doble clic en la compu.
 *
 * Se descarga recién la primera vez que alguien amplía una foto. La mayoría de las
 * visitas no lo hace, y no tiene por qué bajarse el visor con sus estilos.
 */
export default function VisorDeFotos({ fotos, indice, titulo, onCerrar }: Props) {
  return (
    <Lightbox
      open
      close={onCerrar}
      index={indice}
      slides={fotos.map((foto) => ({ src: foto.url, alt: titulo }))}
      plugins={[Counter, Zoom]}
      carousel={{ finite: fotos.length <= 1 }}
      controller={{ closeOnBackdropClick: true }}
      zoom={{ maxZoomPixelRatio: 3 }}
      labels={{
        Previous: 'Foto anterior',
        Next: 'Foto siguiente',
        Close: 'Cerrar',
        'Zoom in': 'Acercar',
        'Zoom out': 'Alejar',
      }}
    />
  )
}
