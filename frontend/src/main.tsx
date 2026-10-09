import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { reportarError } from '@shared/api/client'

// Lo que el límite de errores no ve: los errores de los manejadores de eventos y del
// código que corre fuera del render. Sin `evento.error` es un "Script error." de otro
// origen —una extensión del navegador, casi siempre—, que no dice nada y no es nuestro.
window.addEventListener('error', (evento) => {
  if (evento.error) reportarError(evento.error)
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
