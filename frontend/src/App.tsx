import { lazy, Suspense, type ComponentType } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { SitioPublicoLayout } from '@public/SitioPublicoLayout'
import { FichaPage } from '@public/paginas/FichaPage'
import { HomePage } from '@public/paginas/HomePage'
import { ListadoPage } from '@public/paginas/ListadoPage'
import { RutaProtegida } from '@shared/auth/RutaProtegida'
import { Estado } from '@shared/ui/Estado'

/**
 * Una pantalla que se descarga recién cuando alguien entra a ella.
 *
 * El panel entero va así: el comprador que entra al sitio de una automotora no tiene por
 * qué bajarse las pantallas de cobranza ni de reportes, y cada librería que el panel sume
 * después —gráficos, tablas— pesaría en la vidriera, que es la que se mira desde un
 * celular con 4G.
 */
function diferida<K extends string, M extends Record<K, ComponentType>>(
  cargar: () => Promise<M>,
  nombre: K,
) {
  return lazy(async () => ({ default: (await cargar())[nombre] }))
}

const AdminLayout = diferida(() => import('@admin/AdminLayout'), 'AdminLayout')
const InicioDelPanel = diferida(() => import('@admin/InicioDelPanel'), 'InicioDelPanel')
const LoginPage = diferida(() => import('@admin/LoginPage'), 'LoginPage')
const AutomotorasPage = diferida(() => import('@admin/paginas/AutomotorasPage'), 'AutomotorasPage')
const CatalogoPage = diferida(() => import('@admin/paginas/CatalogoPage'), 'CatalogoPage')
const CobranzaPage = diferida(() => import('@admin/paginas/CobranzaPage'), 'CobranzaPage')
const PlanPage = diferida(() => import('@admin/paginas/PlanPage'), 'PlanPage')
const ConfiguracionPage = diferida(() => import('@admin/paginas/ConfiguracionPage'), 'ConfiguracionPage')
const ReportesPage = diferida(() => import('@admin/paginas/ReportesPage'), 'ReportesPage')
const SolicitudesPage = diferida(() => import('@admin/paginas/SolicitudesPage'), 'SolicitudesPage')
const UsuariosPage = diferida(() => import('@admin/paginas/UsuariosPage'), 'UsuariosPage')
const VehiculoFormPage = diferida(() => import('@admin/paginas/VehiculoFormPage'), 'VehiculoFormPage')
const VehiculosPage = diferida(() => import('@admin/paginas/VehiculosPage'), 'VehiculosPage')

/**
 * El sitio público vive en la raíz y el panel bajo `/admin`.
 *
 * Las mismas pantallas públicas se montan dos veces: en la raíz, que es como entra cada
 * automotora por su dominio propio, y bajo `/t/{slug}`, que es como se trabaja en
 * desarrollo cuando todavía no hay dominios. En los dos casos el tenant lo resuelve el
 * servidor —del Host o del slug— y siempre contra la tabla.
 */
function App() {
  return (
    <BrowserRouter>
      {/* El fallback se ve solo en la primera carga de una pantalla diferida: al navegar,
          React Router hace la transición y deja la pantalla anterior hasta que llega la
          nueva. */}
      <Suspense fallback={<Estado titulo="Cargando…" />}>
        <Routes>
          <Route path="/admin/login" element={<LoginPage />} />

          <Route
            path="/admin"
            element={
              <RutaProtegida>
                <AdminLayout />
              </RutaProtegida>
            }
          >
            {/* Una sola ruta índice: adentro decide por rol. Dos rutas índice hermanas no
                son válidas, y la segunda quedaría muerta sin que nadie lo note. */}
            <Route index element={<InicioDelPanel />} />

            <Route
              path="vehiculos"
              element={
                <RutaProtegida roles={['Owner', 'Seller']}>
                  <VehiculosPage />
                </RutaProtegida>
              }
            />
            <Route
              path="vehiculos/:id"
              element={
                <RutaProtegida roles={['Owner', 'Seller']}>
                  <VehiculoFormPage />
                </RutaProtegida>
              }
            />

            <Route
              path="usuarios"
              element={
                <RutaProtegida roles={['Owner']}>
                  <UsuariosPage />
                </RutaProtegida>
              }
            />
            <Route
              path="configuracion"
              element={
                <RutaProtegida roles={['Owner']}>
                  <ConfiguracionPage />
                </RutaProtegida>
              }
            />
            <Route
              path="reportes"
              element={
                <RutaProtegida roles={['Owner']}>
                  <ReportesPage />
                </RutaProtegida>
              }
            />
            <Route
              path="plan"
              element={
                <RutaProtegida roles={['Owner']}>
                  <PlanPage />
                </RutaProtegida>
              }
            />

            <Route
              path="automotoras"
              element={
                <RutaProtegida roles={['SuperAdmin']}>
                  <AutomotorasPage />
                </RutaProtegida>
              }
            />
            <Route
              path="cobranza"
              element={
                <RutaProtegida roles={['SuperAdmin']}>
                  <CobranzaPage />
                </RutaProtegida>
              }
            />
            <Route
              path="catalogo"
              element={
                <RutaProtegida roles={['SuperAdmin']}>
                  <CatalogoPage />
                </RutaProtegida>
              }
            />
            <Route
              path="solicitudes"
              element={
                <RutaProtegida roles={['SuperAdmin']}>
                  <SolicitudesPage />
                </RutaProtegida>
              }
            />
          </Route>

          {/* Sitio público por dominio propio. */}
          <Route path="/" element={<SitioPublicoLayout />}>
            <Route index element={<HomePage />} />
            <Route path="vehiculos" element={<ListadoPage />} />
            <Route path="vehiculos/:id" element={<FichaPage />} />
          </Route>

          {/* Y el mismo sitio por slug, que es como se trabaja en desarrollo. */}
          <Route path="/t/:slug" element={<SitioPublicoLayout />}>
            <Route index element={<HomePage />} />
            <Route path="vehiculos" element={<ListadoPage />} />
            <Route path="vehiculos/:id" element={<FichaPage />} />
          </Route>

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}

export default App
