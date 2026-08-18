import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AdminLayout } from '@admin/AdminLayout'
import { InicioDelPanel } from '@admin/InicioDelPanel'
import { LoginPage } from '@admin/LoginPage'
import { AutomotorasPage } from '@admin/paginas/AutomotorasPage'
import { CatalogoPage } from '@admin/paginas/CatalogoPage'
import { ConfiguracionPage } from '@admin/paginas/ConfiguracionPage'
import { ReportesPage } from '@admin/paginas/ReportesPage'
import { SolicitudesPage } from '@admin/paginas/SolicitudesPage'
import { UsuariosPage } from '@admin/paginas/UsuariosPage'
import { VehiculoFormPage } from '@admin/paginas/VehiculoFormPage'
import { VehiculosPage } from '@admin/paginas/VehiculosPage'
import { SitioPublicoLayout } from '@public/SitioPublicoLayout'
import { FichaPage } from '@public/paginas/FichaPage'
import { HomePage } from '@public/paginas/HomePage'
import { ListadoPage } from '@public/paginas/ListadoPage'
import { RutaProtegida } from '@shared/auth/RutaProtegida'

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

          <Route path="vehiculos" element={<VehiculosPage />} />
          <Route path="vehiculos/:id" element={<VehiculoFormPage />} />

          <Route
            path="demanda"
            element={
              <RutaProtegida roles={['Owner']}>
                <ReportesPage />
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
            path="automotoras"
            element={
              <RutaProtegida roles={['SuperAdmin']}>
                <AutomotorasPage />
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
    </BrowserRouter>
  )
}

export default App
