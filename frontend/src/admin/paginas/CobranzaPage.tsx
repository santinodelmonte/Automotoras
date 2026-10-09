import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from '@shared/api/client'
import { Esqueleto, Estado } from '@shared/ui/Estado'
import { fecha, precio } from '@shared/ui/formato'
import { ImportadorDeStock } from '@admin/ImportadorDeStock'
import type { EstadoDeCobro, FilaDeCobranza, Plan, SuscripcionDeTenant, Uso } from '@shared/api/types'

/**
 * Cobranza: quién está al día, quién vence esta semana y quién está vencido.
 *
 * Lo primero que se ve, sin filtrar ni buscar nada, es lo urgente: el servidor ya manda
 * las filas ordenadas con los suspendidos arriba. El cobro es manual y vive afuera; acá se
 * registra lo cobrado y con eso se mueve el vencimiento.
 */
export function CobranzaPage() {
  const [filas, setFilas] = useState<FilaDeCobranza[] | null>(null)
  const [planes, setPlanes] = useState<Plan[]>([])
  const [error, setError] = useState<string | null>(null)
  const [abierta, setAbierta] = useState<number | null>(null)

  const cargar = useCallback(async (signal?: AbortSignal) => {
    try {
      const [cobranza, catalogo] = await Promise.all([api.admin.cobranza(signal), api.admin.planes(signal)])
      setFilas(cobranza)
      setPlanes(catalogo)
    } catch (problema) {
      if (signal?.aborted) return
      setError(problema instanceof Error ? problema.message : 'No se pudo cargar la cobranza.')
    }
  }, [])

  useEffect(() => {
    const controlador = new AbortController()
    void cargar(controlador.signal)

    return () => controlador.abort()
  }, [cargar])

  if (error) return <Estado titulo="No pudimos cargar la cobranza" detalle={error} />
  if (!filas) return <Esqueleto className="h-64" />

  const cuenta = (estado: EstadoDeCobro) => filas.filter((f) => f.estado === estado).length

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">Cobranza</h1>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <Contador titulo="Suspendidas" valor={cuenta('Suspendido')} estado="Suspendido" />
        <Contador titulo="En gracia" valor={cuenta('Gracia')} estado="Gracia" />
        <Contador titulo="Vencen esta semana" valor={cuenta('PorVencer')} estado="PorVencer" />
        <Contador titulo="Al día" valor={cuenta('Vigente')} estado="Vigente" />
      </div>

      <ul className="flex flex-col gap-2">
        {filas.map((fila) => (
          <li key={fila.tenantId} className="rounded-xl border border-slate-200 bg-white">
            <button
              type="button"
              onClick={() => setAbierta(abierta === fila.tenantId ? null : fila.tenantId)}
              className="flex w-full flex-wrap items-center justify-between gap-3 p-4 text-left"
            >
              <div className="min-w-0">
                <p className="font-semibold">
                  {fila.nombre}
                  {!fila.activo && <span className="ml-2 text-xs text-slate-500">(apagada)</span>}
                </p>
                <p className="text-sm text-slate-500">
                  {fila.plan ?? 'Sin plan'} · pagado hasta {fecha(fila.pagaHasta)}
                  {fila.diasParaVencer !== null && ` · ${diasEnTexto(fila.diasParaVencer)}`}
                </p>
              </div>

              <div className="flex items-center gap-4 text-sm">
                <span className="text-slate-500">
                  {usoEnTexto(fila.vehiculos)} vehículos · {usoEnTexto(fila.usuarios)} usuarios
                </span>
                <Insignia estado={fila.estado} />
              </div>
            </button>

            {abierta === fila.tenantId && (
              <DetalleDeCobranza tenantId={fila.tenantId} planes={planes} alCambiar={() => void cargar()} />
            )}
          </li>
        ))}
      </ul>

      <EditorDePlanes planes={planes} alGuardar={() => void cargar()} />
    </div>
  )
}

function DetalleDeCobranza({
  tenantId,
  planes,
  alCambiar,
}: {
  tenantId: number
  planes: Plan[]
  alCambiar: () => void
}) {
  const [detalle, setDetalle] = useState<SuscripcionDeTenant | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [trabajando, setTrabajando] = useState(false)

  const hoy = new Date().toISOString().slice(0, 10)
  const [pago, setPago] = useState({ fecha: hoy, monto: '', periodoHasta: '', medio: 'Transferencia', comprobante: '' })
  const [nuevoPlan, setNuevoPlan] = useState('')
  const [motivo, setMotivo] = useState('')

  useEffect(() => {
    const controlador = new AbortController()
    api.admin
      .suscripcion(tenantId, controlador.signal)
      .then(setDetalle)
      .catch((problema: unknown) => {
        if (!controlador.signal.aborted) setMensaje(problema instanceof Error ? problema.message : 'No se pudo cargar.')
      })

    return () => controlador.abort()
  }, [tenantId])

  async function ejecutar(accion: () => Promise<SuscripcionDeTenant>, exito: string) {
    setTrabajando(true)
    setMensaje(null)

    try {
      setDetalle(await accion())
      setMensaje(exito)
      alCambiar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo completar la operación.')
    } finally {
      setTrabajando(false)
    }
  }

  if (!detalle) return <div className="border-t border-slate-100 p-4 text-sm text-slate-500">{mensaje ?? 'Cargando…'}</div>

  const vigente = detalle.vigente

  return (
    <div className="flex flex-col gap-5 border-t border-slate-100 p-4 text-sm">
      {mensaje && <p className="rounded-lg bg-slate-100 px-3 py-2">{mensaje}</p>}

      <div className="grid gap-5 lg:grid-cols-3">
        <form
          className="flex flex-col gap-2"
          onSubmit={(e) => {
            e.preventDefault()
            void ejecutar(
              () =>
                api.admin.registrarPago(tenantId, {
                  fecha: pago.fecha,
                  monto: Number(pago.monto),
                  periodoDesde: null,
                  periodoHasta: pago.periodoHasta,
                  medio: pago.medio,
                  comprobante: pago.comprobante || null,
                }),
              'Pago registrado.',
            )
          }}
        >
          <h3 className="font-semibold">Registrar un cobro</h3>
          {vigente ? (
            <p className="text-slate-500">
              Cubre desde el {fecha(sumarDia(vigente.pagaHasta))}. Plan {vigente.plan.nombre}:{' '}
              {precio(vigente.plan.precioMensual, vigente.plan.moneda)} por mes.
            </p>
          ) : (
            <p className="text-amber-700">Sin plan vigente: asigná uno antes de cobrar.</p>
          )}
          <Campo etiqueta="Fecha del cobro">
            <input type="date" required value={pago.fecha} onChange={(e) => setPago({ ...pago, fecha: e.target.value })} className={entrada} />
          </Campo>
          <Campo etiqueta="Monto">
            <input type="number" min="0" step="0.01" required value={pago.monto} onChange={(e) => setPago({ ...pago, monto: e.target.value })} className={entrada} />
          </Campo>
          <Campo etiqueta="Cubre hasta">
            <input type="date" required value={pago.periodoHasta} onChange={(e) => setPago({ ...pago, periodoHasta: e.target.value })} className={entrada} />
          </Campo>
          <Campo etiqueta="Medio">
            <input required value={pago.medio} onChange={(e) => setPago({ ...pago, medio: e.target.value })} className={entrada} />
          </Campo>
          <Campo etiqueta="Comprobante (opcional)">
            <input value={pago.comprobante} onChange={(e) => setPago({ ...pago, comprobante: e.target.value })} className={entrada} />
          </Campo>
          <button type="submit" disabled={trabajando || !vigente} className={botonPrincipal}>
            Registrar cobro
          </button>
        </form>

        <div className="flex flex-col gap-4">
          <form
            className="flex flex-col gap-2"
            onSubmit={(e) => {
              e.preventDefault()
              void ejecutar(() => api.admin.cambiarPlan(tenantId, nuevoPlan), 'Plan cambiado.')
            }}
          >
            <h3 className="font-semibold">{vigente ? 'Cambiar de plan' : 'Asignar un plan'}</h3>
            <p className="text-slate-500">Lo ya pagado se respeta. La diferencia de precio se cobra aparte.</p>
            <select required value={nuevoPlan} onChange={(e) => setNuevoPlan(e.target.value)} className={entrada}>
              <option value="">Elegí un plan…</option>
              {planes
                .filter((p) => p.activo && p.id !== vigente?.plan.id)
                .map((p) => (
                  <option key={p.id} value={p.codigo}>
                    {p.nombre} — {precio(p.precioMensual, p.moneda)}
                  </option>
                ))}
            </select>
            <button type="submit" disabled={trabajando || !nuevoPlan} className={botonSecundario}>
              {vigente ? 'Cambiar plan' : 'Asignar plan'}
            </button>
          </form>

          {vigente && (
            <form
              className="flex flex-col gap-2"
              onSubmit={(e) => {
                e.preventDefault()
                if (!window.confirm('¿Dar de baja la suscripción? El sitio público queda en mantenimiento; no se borra nada.')) return
                void ejecutar(() => api.admin.darDeBaja(tenantId, hoy, motivo), 'Suscripción dada de baja.')
              }}
            >
              <h3 className="font-semibold">Dar de baja</h3>
              <p className="text-slate-500">Con fecha de hoy. El dueño puede seguir entrando a exportar sus datos.</p>
              <input required placeholder="Motivo" value={motivo} onChange={(e) => setMotivo(e.target.value)} className={entrada} />
              <button type="submit" disabled={trabajando} className="rounded-lg border border-rose-300 px-3 py-1.5 text-rose-700 hover:border-rose-500 disabled:opacity-50">
                Dar de baja
              </button>
            </form>
          )}
        </div>

        <div className="flex flex-col gap-3">
          <h3 className="font-semibold">Historial</h3>
          <ul className="flex flex-col gap-1 text-slate-600">
            {detalle.historial.map((s) => (
              <li key={s.id}>
                <strong>{s.plan.nombre}</strong> desde {fecha(s.inicio)}
                {s.fin ? ` hasta ${fecha(s.fin)}` : ' (vigente)'}
                {s.motivoDeBaja && <span className="text-slate-400"> — {s.motivoDeBaja}</span>}
              </li>
            ))}
          </ul>

          <h3 className="font-semibold">Cobros</h3>
          {detalle.pagos.length === 0 ? (
            <p className="text-slate-500">Todavía no hay cobros.</p>
          ) : (
            <ul className="flex max-h-56 flex-col gap-1 overflow-y-auto text-slate-600">
              {detalle.pagos.map((p) => (
                <li key={p.id}>
                  {fecha(p.fecha)} · {precio(p.monto, p.moneda)} · {p.medio} · cubre {fecha(p.periodoDesde)} a{' '}
                  {fecha(p.periodoHasta)}
                  {p.comprobante && ` · ${p.comprobante}`}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="flex flex-col gap-2 border-t border-slate-100 pt-4">
        <h3 className="font-semibold">Carga inicial de stock</h3>
        <ImportadorDeStock importar={(archivo, confirmar) => api.admin.importarStock(tenantId, archivo, confirmar)} alImportar={alCambiar} />
      </div>
    </div>
  )
}

/**
 * Los planes del catálogo. Cambiar un precio o un tope afecta en el acto a todas las
 * automotoras de ese plan: es lo buscado, una promoción no puede exigir un deploy.
 */
function EditorDePlanes({ planes, alGuardar }: { planes: Plan[]; alGuardar: () => void }) {
  const [editando, setEditando] = useState<Plan | null>(null)
  const [mensaje, setMensaje] = useState<string | null>(null)

  async function guardar(e: React.FormEvent) {
    e.preventDefault()
    if (!editando) return

    try {
      const { id, ...datos } = editando
      await api.admin.actualizarPlan(id, datos)
      setMensaje(`Plan ${editando.nombre} guardado.`)
      setEditando(null)
      alGuardar()
    } catch (problema) {
      setMensaje(problema instanceof ApiError ? problema.message : 'No se pudo guardar el plan.')
    }
  }

  const tope = (valor: string) => (valor === '' ? null : Number(valor))

  return (
    <section className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-5 text-sm">
      <h2 className="font-semibold">Planes</h2>
      <ul className="flex flex-col gap-1">
        {planes.map((p) => (
          <li key={p.id} className="flex flex-wrap items-center justify-between gap-2">
            <span>
              <strong>{p.nombre}</strong> · {precio(p.precioMensual, p.moneda)} · hasta{' '}
              {p.maxVehiculos ?? '∞'} vehículos y {p.maxUsuarios ?? '∞'} usuarios
              {!p.activo && ' · no se vende'}
            </span>
            <button type="button" onClick={() => setEditando(p)} className={botonSecundario}>
              Editar
            </button>
          </li>
        ))}
      </ul>

      {mensaje && <p className="text-slate-600">{mensaje}</p>}

      {editando && (
        <form onSubmit={(e) => void guardar(e)} className="grid gap-3 rounded-lg bg-slate-50 p-4 sm:grid-cols-3">
          <Campo etiqueta="Nombre">
            <input required value={editando.nombre} onChange={(e) => setEditando({ ...editando, nombre: e.target.value })} className={entrada} />
          </Campo>
          <Campo etiqueta="Precio mensual">
            <input type="number" min="0" step="0.01" required value={editando.precioMensual} onChange={(e) => setEditando({ ...editando, precioMensual: Number(e.target.value) })} className={entrada} />
          </Campo>
          <Campo etiqueta="Horas de soporte por mes">
            <input type="number" min="0" required value={editando.horasSoporteMes} onChange={(e) => setEditando({ ...editando, horasSoporteMes: Number(e.target.value) })} className={entrada} />
          </Campo>
          <Campo etiqueta="Tope de vehículos (vacío = sin límite)">
            <input type="number" min="1" value={editando.maxVehiculos ?? ''} onChange={(e) => setEditando({ ...editando, maxVehiculos: tope(e.target.value) })} className={entrada} />
          </Campo>
          <Campo etiqueta="Tope de usuarios (vacío = sin límite)">
            <input type="number" min="1" value={editando.maxUsuarios ?? ''} onChange={(e) => setEditando({ ...editando, maxUsuarios: tope(e.target.value) })} className={entrada} />
          </Campo>
          <div className="flex flex-col gap-1">
            <Casilla etiqueta="Reportes" valor={editando.incluyeReportes} alCambiar={(v) => setEditando({ ...editando, incluyeReportes: v })} />
            <Casilla etiqueta="Benchmark" valor={editando.incluyeBenchmark} alCambiar={(v) => setEditando({ ...editando, incluyeBenchmark: v })} />
            <Casilla etiqueta="Dominio propio" valor={editando.incluyeDominioPropio} alCambiar={(v) => setEditando({ ...editando, incluyeDominioPropio: v })} />
            <Casilla etiqueta="Se vende" valor={editando.activo} alCambiar={(v) => setEditando({ ...editando, activo: v })} />
          </div>
          <div className="flex gap-2 sm:col-span-3">
            <button type="submit" className={botonPrincipal}>
              Guardar
            </button>
            <button type="button" onClick={() => setEditando(null)} className={botonSecundario}>
              Cancelar
            </button>
          </div>
        </form>
      )}
    </section>
  )
}

const coloresDeEstado: Record<EstadoDeCobro, string> = {
  Suspendido: 'bg-rose-100 text-rose-800',
  Gracia: 'bg-amber-100 text-amber-800',
  PorVencer: 'bg-sky-100 text-sky-800',
  Vigente: 'bg-emerald-100 text-emerald-800',
}

const nombresDeEstado: Record<EstadoDeCobro, string> = {
  Suspendido: 'Suspendida',
  Gracia: 'En gracia',
  PorVencer: 'Por vencer',
  Vigente: 'Al día',
}

function Insignia({ estado }: { estado: EstadoDeCobro }) {
  return <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${coloresDeEstado[estado]}`}>{nombresDeEstado[estado]}</span>
}

function Contador({ titulo, valor, estado }: { titulo: string; valor: number; estado: EstadoDeCobro }) {
  return (
    <div className={`rounded-xl p-4 ${valor > 0 ? coloresDeEstado[estado] : 'bg-white text-slate-500'} border border-slate-200`}>
      <p className="text-sm">{titulo}</p>
      <p className="text-3xl font-bold">{valor}</p>
    </div>
  )
}

function Campo({ etiqueta, children }: { etiqueta: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block font-medium text-slate-700">{etiqueta}</span>
      {children}
    </label>
  )
}

function Casilla({ etiqueta, valor, alCambiar }: { etiqueta: string; valor: boolean; alCambiar: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-2">
      <input type="checkbox" checked={valor} onChange={(e) => alCambiar(e.target.checked)} />
      {etiqueta}
    </label>
  )
}

function diasEnTexto(dias: number): string {
  if (dias === 0) return 'vence hoy'
  if (dias > 0) return `vence en ${dias} ${dias === 1 ? 'día' : 'días'}`
  return `vencido hace ${-dias} ${dias === -1 ? 'día' : 'días'}`
}

function usoEnTexto(uso: Uso): string {
  return uso.tope === null ? `${uso.usados}` : `${uso.usados}/${uso.tope}`
}

/** `2026-12-07` → `2026-12-08`, sin pasar por la zona horaria del navegador. */
function sumarDia(iso: string): string {
  const [anio, mes, dia] = iso.split('-').map(Number)
  return new Date(Date.UTC(anio, mes - 1, dia + 1)).toISOString().slice(0, 10)
}

const entrada = 'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm'
const botonPrincipal = 'rounded-lg bg-emerald-600 px-3 py-1.5 font-semibold text-white hover:bg-emerald-500 disabled:opacity-50'
const botonSecundario = 'rounded-lg border border-slate-300 px-3 py-1.5 hover:border-slate-500 disabled:opacity-50'
