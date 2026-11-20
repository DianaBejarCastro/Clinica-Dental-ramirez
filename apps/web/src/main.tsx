import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import ClinicFavicon from './components/ClinicFavicon';
// Los estilos administrativos se cargan desde App.

createRoot(document.getElementById('root')!).render(
  <StrictMode><ClinicFavicon /><App /></StrictMode>,
);
