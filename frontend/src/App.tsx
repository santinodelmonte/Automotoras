import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from '@admin/LoginPage'
import { PanelPage } from '@admin/PanelPage'
import { SitioPublico } from '@public/SitioPublico'
import { RutaProtegida } from '@shared/auth/RutaProtegida'

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Navigate to="/admin" replace />} />

        <Route path="/admin/login" element={<LoginPage />} />
        <Route
          path="/admin"
          element={
            <RutaProtegida roles={['Owner', 'Seller']}>
              <PanelPage />
            </RutaProtegida>
          }
        />

        {/* El slug es el modo de desarrollo. En producción cada automotora entra por su
            dominio y el servidor resuelve el tenant desde el Host, sin prefijo. */}
        <Route path="/t/:slug" element={<SitioPublico />} />
        <Route path="/sitio" element={<SitioPublico />} />

        <Route path="*" element={<Navigate to="/admin" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

export default App
