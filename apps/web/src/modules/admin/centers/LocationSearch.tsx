import { useEffect, useId, useRef, useState } from 'react';
import { api } from '../../../services/api';

interface Result { displayName: string; latitude: number; longitude: number }
export default function LocationSearch({ onSelect }: { onSelect: (latitude: number, longitude: number) => void }) {
  const id = useId(); const [query, setQuery] = useState(''); const [results, setResults] = useState<Result[] | null>(null);
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const generation = useRef(0); const pending = useRef(false); const input = useRef<HTMLInputElement>(null);
  useEffect(() => () => { generation.current++; }, []);
  function close() { generation.current++; setResults(null); setError(''); input.current?.focus(); }
  async function search() {
    if (pending.current) return;
    const q = query.trim(); setError(''); setResults(null);
    if (q.length < 3 || q.length > 250) { setError('Escribe entre 3 y 250 caracteres.'); return; }
    pending.current = true; setBusy(true); const version = ++generation.current;
    try {
      const data = await api<Result[]>(`/api/location/search?q=${encodeURIComponent(q)}`);
      if (generation.current === version) setResults(data.slice(0, 5));
    } catch (e) { if (generation.current === version) setError(e instanceof Error ? e.message : 'No se pudo buscar la ubicación.'); }
    finally { pending.current = false; setBusy(false); }
  }
  return <div className="location-search" onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); close(); } }}>
    <div className="location-search-controls"><label htmlFor={id}>Buscar ubicación<input ref={input} id={id} type="search" maxLength={250} value={query}
      aria-describedby={`${id}-help`} aria-controls={`${id}-results`} onChange={event => { generation.current++; setQuery(event.target.value); setResults(null); setError(''); }}
      onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); event.stopPropagation(); void search(); } }} /></label>
      <button type="button" disabled={busy} onClick={() => void search()}>Buscar</button></div>
    <p className="muted" id={`${id}-help`}>Busca la dirección de la sucursal. Se consulta OpenStreetMap; no incluyas datos personales o confidenciales.</p>
    {busy && <p role="status">Buscando ubicación…</p>}
    {error && <p role="alert">{error}</p>}
    {results?.length === 0 && <p role="status">No se encontraron resultados</p>}
    <ul id={`${id}-results`} className="location-search-results" aria-label="Resultados de ubicación" hidden={!results?.length}>
      {results?.map((result, index) => <li key={index}><button type="button" className="secondary" onClick={() => {
        onSelect(result.latitude, result.longitude); close();
      }}>{result.displayName}</button></li>)}
    </ul>
  </div>;
}
