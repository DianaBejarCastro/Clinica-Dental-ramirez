import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
// Los estilos administrativos se cargan desde App.

createRoot(document.getElementById('root')!).render(
  <StrictMode><App /></StrictMode>,
);
