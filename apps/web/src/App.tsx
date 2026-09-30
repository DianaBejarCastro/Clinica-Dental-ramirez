import { useEffect, useState } from 'react';

const apiUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';

export default function App() {
  const [status, setStatus] = useState('Comprobando conexión…');

  useEffect(() => {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => controller.abort(), 5000);
    fetch(`${apiUrl}/health`, { signal: controller.signal })
      .then(response => {
        if (!response.ok) throw new Error('API no disponible');
        setStatus('API disponible');
      })
      .catch(() => {
        if (!cancelled) setStatus('API sin conexión');
      });
    let cancelled = false;
    return () => {
      cancelled = true;
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, []);

  return (
    <main>
      <header><span className="mark" aria-hidden="true">R</span><span>CLÍNICA DENTAL <strong>RAMÍREZ</strong></span></header>
      <section>
        <p className="eyebrow">BASE DEL PROYECTO · V0.1.0</p>
        <h1>Un nuevo comienzo<br />para nuestra clínica.</h1>
        <p className="intro">El espacio digital de Clínica Dental RAMÍREZ está en preparación.</p>
        <div className="status" role="status"><span aria-hidden="true">●</span> {status}</div>
      </section>
      <footer>Clínica Dental RAMÍREZ <span>Infraestructura inicial</span></footer>
    </main>
  );
}
