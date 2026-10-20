import { useEffect, useId, useRef, useState } from 'react';
import { api } from '../../../services/api';
import './patient-autocomplete.css';

export interface PatientSelection { patientId: string; userId?: string; firstName: string; lastName: string; ci?: string | null; phone?: string | null; email?: string | null }
const name = (p: PatientSelection) => `${p.firstName} ${p.lastName}`.trim();
export default function PatientAutocomplete({ initial = null, disabled = false, onChange, searchEndpoint = '/api/admin/patients/search' }: {
  initial?: PatientSelection | null; disabled?: boolean; onChange: (patient: PatientSelection | null) => void; searchEndpoint?: string;
}) {
  const id = useId(); const input = useRef<HTMLInputElement>(null); const generation = useRef(0);
  const [text, setText] = useState(initial ? name(initial) : ''); const [selected, setSelected] = useState(initial);
  const [items, setItems] = useState<PatientSelection[]>([]); const [open, setOpen] = useState(false); const [active, setActive] = useState(-1);
  const [loading, setLoading] = useState(false); const [error, setError] = useState(''); const [searched, setSearched] = useState(false);
  const query = text.trim().replace(/\s+/g, ' ');
  useEffect(() => {
    const version = ++generation.current; setItems([]); setActive(-1); setError(''); setSearched(false); setLoading(false);
    if (selected || query.length < 2 || disabled || !open) return;
    const timer = window.setTimeout(() => {
      setLoading(true);
      api<PatientSelection[]>(`${searchEndpoint}?${new URLSearchParams({ q: query, limit: '10' })}`)
        .then(results => { if (generation.current === version) { setItems(results); setSearched(true); } })
        .catch(e => { if (generation.current === version) setError(e.message); })
        .finally(() => { if (generation.current === version) setLoading(false); });
    }, 300);
    return () => { window.clearTimeout(timer); generation.current++; };
  }, [query, selected, disabled, open, searchEndpoint]);
  function choose(patient: PatientSelection) {
    generation.current++; setSelected(patient); setText(name(patient)); setOpen(false); setItems([]); setLoading(false); onChange(patient); input.current?.focus();
  }
  function change(value: string) {
    generation.current++; setText(value); setSelected(null); setItems([]); setActive(-1); setError(''); setSearched(false); setLoading(false); setOpen(true); onChange(null);
  }
  return <div className="patient-autocomplete" onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget)) { generation.current++; setOpen(false); } }}>
    <label htmlFor={id}>Paciente</label>
    <input ref={input} id={id} role="combobox" aria-autocomplete="list" aria-expanded={open && items.length > 0}
      aria-controls={`${id}-results`} aria-activedescendant={open && active >= 0 ? `${id}-option-${active}` : undefined}
      aria-describedby={`${id}-status`} aria-required="true" autoComplete="off" maxLength={200} disabled={disabled}
      placeholder="Buscar por CI, nombre, email o teléfono…" value={text}
      onChange={e => change(e.target.value)} onFocus={() => { if (!selected) setOpen(true); }}
      onKeyDown={e => {
        if (e.key === 'Escape') { e.preventDefault(); generation.current++; setOpen(false); }
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
          e.preventDefault(); setOpen(true); setActive(index => items.length ? (index + (e.key === 'ArrowDown' ? 1 : -1) + items.length) % items.length : -1);
        }
        if (e.key === 'Enter') { e.preventDefault(); if (open && active >= 0 && items[active]) choose(items[active]); }
      }} />
    {selected && <div className="patient-selection"><small>Paciente seleccionado · {selected.ci ? `CI: ${selected.ci}` : name(selected)}</small>
      <button type="button" className="secondary" disabled={disabled} onClick={() => { change(''); input.current?.focus(); }}>Cambiar paciente</button></div>}
    <div id={`${id}-status`} role="status" aria-live="polite">
      {open && loading && 'Buscando…'}{open && !loading && searched && !items.length && 'No se encontraron coincidencias.'}
      {!selected && query.length < 2 && 'Escribe al menos 2 caracteres y selecciona un paciente.'}
    </div>
    {open && error && <p role="alert" className="error">{error}</p>}
    {open && items.length > 0 && <ul id={`${id}-results`} role="listbox" aria-label="Pacientes encontrados" className="patient-results">
      {items.map((p, index) => <li key={p.patientId} id={`${id}-option-${index}`} role="option" aria-selected={active === index}
        onMouseDown={e => e.preventDefault()} onClick={() => choose(p)} onMouseMove={() => setActive(index)}>
        <strong>{name(p)}</strong><small>CI: {p.ci ?? '—'} · Teléfono: {p.phone ?? '—'}</small>{p.email && <small>{p.email}</small>}
      </li>)}
    </ul>}
  </div>;
}
