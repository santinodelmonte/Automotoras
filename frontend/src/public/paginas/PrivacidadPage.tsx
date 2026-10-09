import { useEffect, type ReactNode } from 'react'
import { useSitio } from '@public/TenantContexto'

/**
 * Política de privacidad del sitio de cada automotora.
 *
 * Describe lo que el sistema hace de verdad, no un texto genérico: si cambia lo que se
 * mide (ver `Evento`, `Busqueda` y `shared/analitica/sesion.ts`), esto cambia con él. La
 * responsable del sitio es la automotora, por eso habla en su nombre y remite a sus datos
 * de contacto para ejercer los derechos de la Ley 18.331.
 */
export function PrivacidadPage() {
  const { tenant } = useSitio()

  useEffect(() => {
    document.title = `Privacidad — ${tenant.nombre}`
  }, [tenant.nombre])

  const medios = [
    tenant.telefono && `por teléfono al ${tenant.telefono}`,
    tenant.whatsapp && 'por WhatsApp',
    tenant.direccion && `en nuestro local de ${tenant.direccion}`,
  ].filter(Boolean)
  const contacto = medios.length > 1 ? `${medios.slice(0, -1).join(', ')} o ${medios.at(-1)}` : medios[0]

  return (
    <article className="mx-auto max-w-3xl px-4 py-12 sm:px-6 sm:py-16">
      <h1 className="text-3xl font-bold tracking-tight text-slate-900">Política de privacidad</h1>
      <p className="mt-3 text-slate-600">
        Este sitio es de {tenant.nombre}. Acá contamos qué información se registra cuando lo
        visitás y para qué.
      </p>

      <Seccion titulo="Lo que no hacemos">
        <p>
          No te pedimos que te registres ni que completes formularios con tus datos. No usamos
          cookies, ni publicidad, ni herramientas de seguimiento de terceros. No guardamos tu
          dirección IP, tu navegador ni la página desde la que llegaste.
        </p>
      </Seccion>

      <Seccion titulo="Lo que sí medimos">
        <p>Para saber qué vehículos interesan y qué stock conviene traer, registramos de forma anónima:</p>
        <ul className="mt-3 list-disc space-y-1.5 pl-5">
          <li>qué vehículos se miran y cuáles aparecen en los listados;</li>
          <li>los clics en los botones de WhatsApp y de teléfono;</li>
          <li>las búsquedas y los filtros que se usan, sobre todo los que no encuentran resultados.</li>
        </ul>
        <p className="mt-3">
          Para contar visitas distintas sin saber quién es nadie, tu navegador guarda un
          identificador al azar que se borra al cerrar la pestaña. No permite reconocerte de
          un día para otro ni vincular la visita con tu persona.
        </p>
      </Seccion>

      <Seccion titulo="Cuando nos contactás">
        <p>
          Los botones de WhatsApp y de teléfono abren esas aplicaciones: la conversación pasa
          por ellas y se rige por sus propias políticas. Lo que nos cuentes lo usamos solo para
          responder tu consulta.
        </p>
      </Seccion>

      <Seccion titulo="Cuánto tiempo se guarda">
        <p>
          El registro de visitas y búsquedas se borra a los 24 meses.
        </p>
      </Seccion>

      <Seccion titulo="Tus derechos">
        <p>
          La Ley 18.331 de Protección de Datos Personales te da derecho a saber qué datos
          tuyos tenemos y a pedir que se corrijan o se eliminen.
          {contacto ? ` Podés pedirlo ${contacto}.` : ' Podés pedirlo por cualquiera de nuestros medios de contacto.'}
        </p>
      </Seccion>
    </article>
  )
}

function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <section className="mt-10">
      <h2 className="text-lg font-semibold text-slate-900">{titulo}</h2>
      <div className="mt-3 leading-relaxed text-slate-600">{children}</div>
    </section>
  )
}
