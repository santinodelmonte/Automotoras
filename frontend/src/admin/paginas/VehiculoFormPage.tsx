import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { api, ApiError } from '@shared/api/client'
import { useSesion } from '@shared/auth/useSesion'
import { Estado } from '@shared/ui/Estado'
import { etiqueta, paraInputDate } from '@shared/ui/formato'
import { GaleriaDeFotos } from '@admin/GaleriaDeFotos'
import { CambiarEstado } from '@admin/CambiarEstado'
import { Aviso, Campo, Pagina, Seccion } from '@admin/ui/Pagina'
import { EntradaNumerica } from '@admin/ui/EntradaNumerica'
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
    <Pagina
      titulo={
        <span className="flex flex-col gap-2">
          <Link
            to="/admin/vehiculos"
            className="inline-flex w-fit items-center gap-1 text-sm font-medium text-slate-500 hover:text-slate-800"
          >
            <ArrowLeft aria-hidden className="size-4" />
            Vehículos
          </Link>
          {esNuevo ? 'Cargar vehículo' : `${vehiculo?.marca ?? ''} ${vehiculo?.modelo ?? ''} ${vehiculo?.anio ?? ''}`}
        </span>
      }
      descripcion={
        esNuevo
          ? 'Primero los datos; las fotos se cargan en el paso siguiente.'
          : 'Los cambios se ven en el sitio apenas los guardás.'
      }
      acciones={
        vehiculo && (
          <CambiarEstado
            vehiculo={vehiculo}
            onCambio={(actualizado) => {
              setVehiculo(actualizado)
              setDatos((previos) => ({ ...previos, destacado: actualizado.destacado }))
            }}
          />
        )
      }
    >
      <form onSubmit={guardar} className="flex flex-col gap-5">
        <Seccion titulo="El vehículo" descripcion="Marca y modelo salen del catálogo, para que las búsquedas y los reportes coincidan.">
          <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Marca" errores={errores.ModeloId}>
            <select
              required
              value={marcaId || ''}
              onChange={(e) => {
                setMarcaId(Number(e.target.value))
                // Cambiar de marca invalida el modelo y la versión elegidos.
                setDatos((previos) => ({ ...previos, modeloId: 0, versionId: null }))
              }}
              className="panel-entrada"
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
              className="panel-entrada"
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
              className="panel-entrada"
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
              className="panel-entrada"
            />
          </Campo>

          <Campo etiqueta="Kilometraje" errores={errores.Kilometraje}>
            <EntradaNumerica
              required
              decimales={false}
              sufijo="km"
              valor={datos.kilometraje}
              onCambio={(valor) => cambiar('kilometraje', valor ?? 0)}
            />
          </Campo>

          <Campo etiqueta="Combustible">
            <select
              value={datos.combustible}
              onChange={(e) => cambiar('combustible', e.target.value)}
              className="panel-entrada"
            >
              {(opciones?.combustibles ?? [datos.combustible]).map((valor) => (
                <option key={valor} value={valor}>
                  {etiqueta(valor)}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Transmisión">
            <select
              value={datos.transmision}
              onChange={(e) => cambiar('transmision', e.target.value)}
              className="panel-entrada"
            >
              {(opciones?.transmisiones ?? [datos.transmision]).map((valor) => (
                <option key={valor} value={valor}>
                  {etiqueta(valor)}
                </option>
              ))}
            </select>
          </Campo>

          <Campo etiqueta="Color">
            <input
              value={datos.color ?? ''}
              onChange={(e) => cambiar('color', e.target.value || null)}
              className="panel-entrada"
            />
          </Campo>

          <Campo etiqueta="Puertas" errores={errores.Puertas}>
            <input
              type="number"
              value={datos.puertas ?? ''}
              onChange={(e) => cambiar('puertas', e.target.value ? Number(e.target.value) : null)}
              className="panel-entrada"
            />
          </Campo>

          <Campo etiqueta="Motor">
            <input
              placeholder="1.6"
              value={datos.motor ?? ''}
              onChange={(e) => cambiar('motor', e.target.value || null)}
              className="panel-entrada"
            />
          </Campo>
          </div>
        </Seccion>

        <Seccion titulo="Precio y publicación">
          <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Precio" errores={errores.Precio}>
            <EntradaNumerica
              required
              moneda={datos.moneda}
              valor={datos.precio || null}
              onCambio={(valor) => cambiar('precio', valor ?? 0)}
            />
          </Campo>

          <Campo etiqueta="Moneda">
            <select
              value={datos.moneda}
              onChange={(e) => cambiar('moneda', e.target.value)}
              className="panel-entrada"
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
            <Campo etiqueta="Precio de costo" ayuda="Solo lo ven los dueños. No sale en el sitio." errores={errores.PrecioCosto}>
              <EntradaNumerica
                moneda={datos.moneda}
                valor={datos.precioCosto}
                onCambio={(valor) => cambiar('precioCosto', valor)}
              />
            </Campo>
          )}

          <Campo etiqueta="Fecha de publicación" ayuda="Vacía, se toma la de hoy. Cuenta para los días en góndola." errores={errores.FechaPublicacion}>
            <input
              type="date"
              value={paraInputDate(datos.fechaPublicacion)}
              onChange={(e) => cambiar('fechaPublicacion', e.target.value || null)}
              className="panel-entrada"
            />
          </Campo>

          <label className="flex cursor-pointer items-start gap-3 rounded-xl border border-slate-200 p-3 text-sm transition hover:bg-slate-50 sm:col-span-2">
            <input
              type="checkbox"
              checked={datos.destacado}
              onChange={(e) => cambiar('destacado', e.target.checked)}
              className="mt-0.5 size-4 accent-emerald-600"
            />
            <span>
              <span className="block font-medium text-slate-900">Destacar en la home del sitio</span>
              <span className="text-slate-500">Aparece primero, con una etiqueta de destacado.</span>
            </span>
          </label>

          <Campo etiqueta="Descripción" className="sm:col-span-2" errores={errores.Descripcion}>
            <textarea
              rows={4}
              value={datos.descripcion ?? ''}
              onChange={(e) => cambiar('descripcion', e.target.value || null)}
              className="panel-entrada"
            />
          </Campo>
          </div>
        </Seccion>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="submit"
            disabled={guardando}
            className="panel-boton"
          >
            {guardando ? 'Guardando…' : esNuevo ? 'Crear y cargar fotos' : 'Guardar cambios'}
          </button>

          {mensaje && (
            <Aviso nivel={mensaje === 'Guardado.' ? 'ok' : 'grave'}>{mensaje}</Aviso>
          )}
        </div>
      </form>

      {!esNuevo && vehiculo && (
        <GaleriaDeFotos vehiculoId={vehiculo.id} fotos={fotos} onCambio={setFotos} />
      )}
    </Pagina>
  )
}
