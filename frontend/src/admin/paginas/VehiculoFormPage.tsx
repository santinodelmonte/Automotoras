import { useCallback, useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, ApiError } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import { Estado } from '@shared/ui/Estado'
import { paraInputDate } from '@shared/ui/formato'
import { GaleriaDeFotos } from '@admin/GaleriaDeFotos'
import { CambiarEstado } from '@admin/CambiarEstado'
import type {
  GuardarVehiculoRequest,
  Marca,
  Modelo,
  OpcionesDeCatalogo,
  Vehiculo,
  VehiculoFoto,
  VersionVehiculo,
} from '@shared/api/types'

const VACIO: GuardarVehiculoRequest = {
  modeloId: 0,
  versionId: null,
  anio: new Date().getFullYear(),
  kilometraje: 0,
  combustible: 'Nafta',
  transmision: 'Manual',
  color: null,
  puertas: null,
  motor: null,
  precio: 0,
  moneda: 'Usd',
  descripcion: null,
  destacado: false,
  precioCosto: null,
  fechaPublicacion: null,
}

export function VehiculoFormPage() {
  const { id } = useParams<{ id: string }>()
  const navegar = useNavigate()
  const sesion = useSesion()

  const esNuevo = id === 'nuevo'
  const vehiculoId = esNuevo ? 0 : Number(id)
  const esOwner = sesion?.usuario.rol === 'Owner'

  const [datos, setDatos] = useState<GuardarVehiculoRequest>(VACIO)
  const [vehiculo, setVehiculo] = useState<Vehiculo | null>(null)
  const [fotos, setFotos] = useState<VehiculoFoto[]>([])

  const [marcas, setMarcas] = useState<Marca[]>([])
  const [marcaId, setMarcaId] = useState<number>(0)
  const [modelos, setModelos] = useState<Modelo[]>([])
  const [versiones, setVersiones] = useState<VersionVehiculo[]>([])
  const [opciones, setOpciones] = useState<OpcionesDeCatalogo | null>(null)

  const [errores, setErrores] = useState<Record<string, string[]>>({})
  const [mensaje, setMensaje] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)
  const [noExiste, setNoExiste] = useState(false)

  useEffect(() => {
    const controlador = new AbortController()

    void Promise.all([
      api.catalogo.marcas(controlador.signal).then(setMarcas),
      api.catalogo.opciones(controlador.signal).then(setOpciones),
    ]).catch(() => undefined)

    return () => controlador.abort()
  }, [])

  useEffect(() => {
    if (esNuevo) return

    const controlador = new AbortController()

    api.vehiculos
      .obtener(vehiculoId, controlador.signal)
      .then((encontrado) => {
        setVehiculo(encontrado)
        setFotos(encontrado.fotos)
        setMarcaId(encontrado.marcaId)
        setDatos({
          modeloId: encontrado.modeloId,
          versionId: encontrado.versionId,
          anio: encontrado.anio,
          kilometraje: encontrado.kilometraje,
          combustible: encontrado.combustible,
          transmision: encontrado.transmision,
          color: encontrado.color,
          puertas: encontrado.puertas,
          motor: encontrado.motor,
          precio: encontrado.precio,
          moneda: encontrado.moneda,
          descripcion: encontrado.descripcion,
          destacado: encontrado.destacado,
          precioCosto: encontrado.precioCosto,
          fechaPublicacion: encontrado.fechaPublicacion,
        })
      })
      .catch((problema: unknown) => {
        if (controlador.signal.aborted) return
        if (problema instanceof ApiError && problema.status === 404) setNoExiste(true)
      })

    return () => controlador.abort()
  }, [esNuevo, vehiculoId])

  // Selects encadenados: la marca decide los modelos y el modelo decide las versiones.
  useEffect(() => {
    if (!marcaId) {
      setModelos([])
      return
    }

    const controlador = new AbortController()
    api.catalogo.modelos(marcaId, controlador.signal).then(setModelos).catch(() => undefined)

    return () => controlador.abort()
  }, [marcaId])

  useEffect(() => {
    if (!datos.modeloId) {
      setVersiones([])
      return
    }

    const controlador = new AbortController()
    api.catalogo
      .versiones(datos.modeloId, controlador.signal)
      .then(setVersiones)
      .catch(() => undefined)

    return () => controlador.abort()
  }, [datos.modeloId])

  const cambiar = useCallback(<C extends keyof GuardarVehiculoRequest>(
    clave: C,
    valor: GuardarVehiculoRequest[C],
  ) => {
    setDatos((previos) => ({ ...previos, [clave]: valor }))
  }, [])

  async function guardar(evento: React.FormEvent) {
    evento.preventDefault()
    setErrores({})
    setMensaje(null)
    setGuardando(true)

    try {
      const guardado = esNuevo
        ? await api.vehiculos.crear(datos)
        : await api.vehiculos.actualizar(vehiculoId, datos)

      if (esNuevo) {
        // Se navega a la edición para poder cargarle las fotos: un vehículo sin fotos casi
        // no recibe consultas, y pedirlas antes de que exista el id no se puede.
        navegar(`/admin/vehiculos/${guardado.id}`, { replace: true })
        return
      }

      setVehiculo(guardado)
      setMensaje('Guardado.')
    } catch (problema) {
      if (problema instanceof ApiError) {
        setErrores(problema.erroresPorCampo)
        setMensaje(problema.message)
      } else {
        setMensaje('No se pudo guardar.')
      }
    } finally {
      setGuardando(false)
    }
  }

  if (noExiste) {
    return (
      <Estado
        titulo="No existe ese vehículo"
        detalle="Puede que lo haya borrado otra persona, o que sea de otra automotora."
      />
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">
          {esNuevo ? 'Cargar vehículo' : `${vehiculo?.marca ?? ''} ${vehiculo?.modelo ?? ''}`}
        </h1>

        {vehiculo && (
          <CambiarEstado
            vehiculo={vehiculo}
            onCambio={(actualizado) => {
              setVehiculo(actualizado)
              setDatos((previos) => ({ ...previos, destacado: actualizado.destacado }))
            }}
          />
        )}
      </div>

      <form onSubmit={guardar} className="flex flex-col gap-5">
        <section className="grid gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:grid-cols-2">
          <Campo etiqueta="Marca" errores={errores.ModeloId}>
            <select
              required
              value={marcaId || ''}
              onChange={(e) => {
                setMarcaId(Number(e.target.value))
                // Cambiar de marca invalida el modelo y la versión elegidos.
                setDatos((previos) => ({ ...previos, modeloId: 0, versionId: null }))
              }}
              className={entrada}
            >
              <option value="">Elegí una marca</option>
              {marcas.map((marca) => (
                <option key={marca.id} value={marca.id}>
                  {marca.nombre}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Modelo" errores={errores.ModeloId}>
            <select
              required
              value={datos.modeloId || ''}
              disabled={!marcaId}
              onChange={(e) =>
                setDatos((previos) => ({
                  ...previos,
                  modeloId: Number(e.target.value),
                  versionId: null,
                }))
              }
              className={entrada}
            >
              <option value="">Elegí un modelo</option>
              {modelos.map((modelo) => (
                <option key={modelo.id} value={modelo.id}>
                  {modelo.nombre}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Versión (opcional)">
            <select
              value={datos.versionId ?? ''}
              disabled={!datos.modeloId || versiones.length === 0}
              onChange={(e) => cambiar('versionId', e.target.value ? Number(e.target.value) : null)}
              className={entrada}
            >
              <option value="">Sin versión</option>
              {versiones.map((version) => (
                <option key={version.id} value={version.id}>
                  {version.nombre}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Año" errores={errores.Anio}>
            <input
              type="number"
              required
              value={datos.anio}
              onChange={(e) => cambiar('anio', Number(e.target.value))}
              className={entrada}
            />
          </Campo>

          <Campo etiqueta="Kilometraje" errores={errores.Kilometraje}>
            <input
              type="number"
              required
              min={0}
              value={datos.kilometraje}
              onChange={(e) => cambiar('kilometraje', Number(e.target.value))}
              className={entrada}
            />
          </Campo>

          <Campo etiqueta="Combustible">
            <select
              value={datos.combustible}
              onChange={(e) => cambiar('combustible', e.target.value)}
              className={entrada}
            >
              {(opciones?.combustibles ?? [datos.combustible]).map((valor) => (
                <option key={valor} value={valor}>
                  {valor}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Transmisión">
            <select
              value={datos.transmision}
              onChange={(e) => cambiar('transmision', e.target.value)}
              className={entrada}
            >
              {(opciones?.transmisiones ?? [datos.transmision]).map((valor) => (
                <option key={valor} value={valor}>
                  {valor}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Color">
            <input
              value={datos.color ?? ''}
              onChange={(e) => cambiar('color', e.target.value || null)}
              className={entrada}
            />
          </Campo>

          <Campo etiqueta="Puertas" errores={errores.Puertas}>
            <input
              type="number"
              value={datos.puertas ?? ''}
              onChange={(e) => cambiar('puertas', e.target.value ? Number(e.target.value) : null)}
              className={entrada}
            />
          </Campo>

          <Campo etiqueta="Motor">
            <input
              placeholder="1.6"
              value={datos.motor ?? ''}
              onChange={(e) => cambiar('motor', e.target.value || null)}
              className={entrada}
            />
          </Campo>
        </section>

        <section className="grid gap-4 rounded-xl border border-slate-200 bg-white p-5 sm:grid-cols-2">
          <Campo etiqueta="Precio" errores={errores.Precio}>
            <input
              type="number"
              required
              min={1}
              step="0.01"
              value={datos.precio || ''}
              onChange={(e) => cambiar('precio', Number(e.target.value))}
              className={entrada}
            />
          </Campo>

          <Campo etiqueta="Moneda">
            <select
              value={datos.moneda}
              onChange={(e) => cambiar('moneda', e.target.value)}
              className={entrada}
            >
              {(opciones?.monedas ?? [datos.moneda]).map((valor) => (
                <option key={valor} value={valor}>
                  {valor.toUpperCase()}
                </option>
              ))}
            </select>
          </Campo>

          {/* El precio de costo es del dueño. El servidor tampoco lo acepta de un Seller. */}
          {esOwner && (
            <Campo etiqueta="Precio de costo" errores={errores.PrecioCosto}>
              <input
                type="number"
                min={0}
                step="0.01"
                value={datos.precioCosto ?? ''}
                onChange={(e) =>
                  cambiar('precioCosto', e.target.value ? Number(e.target.value) : null)
                }
                className={entrada}
              />
            </Campo>
          )}

          <Campo etiqueta="Fecha de publicación" errores={errores.FechaPublicacion}>
            <input
              type="date"
              value={paraInputDate(datos.fechaPublicacion)}
              onChange={(e) => cambiar('fechaPublicacion', e.target.value || null)}
              className={entrada}
            />
          </Campo>

          <label className="flex items-center gap-2 text-sm sm:col-span-2">
            <input
              type="checkbox"
              checked={datos.destacado}
              onChange={(e) => cambiar('destacado', e.target.checked)}
              className="size-4"
            />
            Destacar en la home del sitio
          </label>

          <Campo etiqueta="Descripción" className="sm:col-span-2" errores={errores.Descripcion}>
            <textarea
              rows={4}
              value={datos.descripcion ?? ''}
              onChange={(e) => cambiar('descripcion', e.target.value || null)}
              className={entrada}
            />
          </Campo>
        </section>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="submit"
            disabled={guardando}
            className="rounded-lg bg-emerald-600 px-5 py-2.5 font-semibold text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {guardando ? 'Guardando…' : esNuevo ? 'Crear y cargar fotos' : 'Guardar cambios'}
          </button>

          {mensaje && <p className="text-sm text-slate-600">{mensaje}</p>}
        </div>
      </form>

      {!esNuevo && vehiculo && (
        <GaleriaDeFotos vehiculoId={vehiculo.id} fotos={fotos} onCambio={setFotos} />
      )}
    </div>
  )
}

const entrada =
  'w-full rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:bg-slate-100 disabled:text-slate-400'

function Campo({
  etiqueta,
  children,
  errores,
  className = '',
}: {
  etiqueta: string
  children: React.ReactNode
  errores?: string[]
  className?: string
}) {
  return (
    <label className={`block text-sm ${className}`}>
      <span className="mb-1 block font-medium text-slate-700">{etiqueta}</span>
      {children}
      {errores?.map((error) => (
        <span key={error} className="mt-1 block text-xs text-rose-600">
          {error}
        </span>
      ))}
    </label>
  )
}
