import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, guardarArchivo } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { fecha, precio } from '@shared/ui/formato'
import { ImportadorDeStock } from '@admin/ImportadorDeStock'
import type { SituacionDelPlan, Uso } from '@shared/api/types'

/**
 * El plan del dueño: qué incluye, cuánto usa, hasta cuándo está pago, y sus datos.
 *
 * La exportación está acá y no escondida: los datos son de la automotora y se los puede
 * llevar cuando quiera, no solo el día que se da de baja.
 */
export function PlanPage() {
  const [situacion, setSituacion] = useState<SituacionDelPlan | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [exportando, setExportando] = useState(false)
  const [mensaje, setMensaje] = useState<string | null>(null)

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      setSituacion(await api.tenant.plan(signal))
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudo cargar el plan.')
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [cargar])

  async function exportar() {
    setExportando(true)
    setMensaje(null)

    try {
      const hoy = new Date().toISOString().slice(0, 10)
      guardarArchivo(await api.tenant.exportar(), `exportacion-${hoy}.zip`)
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo generar la exportación.')
    } finally {
      setExportando(false)
    }
  }

  if (error) return <Estado titulo="No pudimos cargar el plan" detalle={error} />
  if (!situacion) return <Esqueleto className="h-64" />

  const { plan } = situacion

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">Mi plan</h1>

      <section className="grid gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:grid-cols-3">
        <div>
          <p className="text-sm text-slate-500">Plan</p>
          <p className="text-2xl font-bold">{plan?.nombre ?? 'Sin plan vigente'}</p>
          {plan && (
            <p className="text-sm text-slate-500">
              {precio(plan.precioMensual, plan.moneda)} por mes, más IVA
            </p>
          )}
        </div>

        <div>
          <p className="text-sm text-slate-500">Pago cubierto hasta</p>
          <p className="text-2xl font-bold">{fecha(situacion.pagaHasta)}</p>
          <p className="text-sm text-slate-500">{textoDelEstado[situacion.estado]}</p>
        </div>

        <div className="flex flex-col gap-2">
          <Medidor titulo="Vehículos publicados" uso={situacion.vehiculos} />
          <Medidor titulo="Usuarios" uso={situacion.usuarios} />
        </div>
      </section>

      {plan && (
        <section className="rounded-xl border border-slate-200 bg-white p-5 text-sm">
          <h2 className="mb-3 font-semibold">Qué incluye</h2>
          <ul className="grid gap-1 sm:grid-cols-2">
            <Incluye si>Sitio público con tu marca y panel de administración</Incluye>
            <Incluye si={plan.incluyeReportes}>Reportes de demanda y sugerencias de compra</Incluye>
            <Incluye si={plan.incluyeBenchmark}>Comparación anónima contra el mercado</Incluye>
            <Incluye si={plan.incluyeDominioPropio}>Dominio propio</Incluye>
            <Incluye si={plan.horasSoporteMes > 0}>
              {plan.horasSoporteMes > 0 ? `${plan.horasSoporteMes} h de ajustes por mes` : 'Horas de ajustes'}
            </Incluye>
          </ul>
          <p className="mt-3 text-slate-500">
            Para cambiar de plan, escribinos: se puede subir o bajar en cualquier momento.
          </p>
        </section>
      )}

      <section className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">Tus datos</h2>
        <p className="text-sm text-slate-500">
          Un ZIP con todo el stock (incluido lo vendido), las fotos, las consultas y las vistas
          por día, y las búsquedas que no encontraron resultado. Se abre en Excel o Google
          Sheets.
        </p>
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => void exportar()}
            disabled={exportando}
            className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700 disabled:opacity-50"
          >
            {exportando ? 'Generando…' : 'Descargar todos mis datos'}
          </button>
          {mensaje && <p className="text-sm text-rose-700">{mensaje}</p>}
        </div>
      </section>

      <section className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-5">
        <h2 className="font-semibold">Cargar stock desde una planilla</h2>
        <ImportadorDeStock importar={api.vehiculos.importar} alImportar={() => void cargar()} />
      </section>
    </div>
  )
}

const textoDelEstado: Record<SituacionDelPlan['estado'], string> = {
  Vigente: 'Al día',
  PorVencer: 'Por vencer',
  Gracia: 'Vencido: el sitio sigue publicado unos días',
  Suspendido: 'Sitio en mantenimiento',
}

function Medidor({ titulo, uso }: { titulo: string; uso: Uso }) {
  const porcentaje = uso.tope ? Math.min(100, Math.round((uso.usados / uso.tope) * 100)) : 0
  const color = uso.tope && uso.usados >= uso.tope ? 'bg-rose-500' : uso.cercaDelTope ? 'bg-amber-500' : 'bg-emerald-500'

  return (
    <div>
      <p className="flex justify-between text-sm">
        <span className="text-slate-500">{titulo}</span>
        <span className="font-semibold">
          {uso.usados} {uso.tope === null ? '· sin límite' : `de ${uso.tope}`}
        </span>
      </p>
      {uso.tope !== null && (
        <div className="mt-1 h-2 rounded-full bg-slate-100">
          <div className={`h-2 rounded-full ${color}`} style={{ width: `${porcentaje}%` }} />
        </div>
      )}
    </div>
  )
}

function Incluye({ si, children }: { si: boolean; children: React.ReactNode }) {
  return (
    <li className={si ? 'text-slate-800' : 'text-slate-400 line-through'}>
      {si ? '✓' : '—'} {children}
    </li>
  )
}
