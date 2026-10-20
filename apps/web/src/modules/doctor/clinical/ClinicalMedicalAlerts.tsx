import { useEffect, useId, useRef, useState } from 'react';
import type { History, Review } from './types';

const categories = [
  { key: 'allergies', title: 'Alergias', empty: 'Sin alergias registradas; ausencia no confirmada.' },
  { key: 'conditions', title: 'Enfermedades', empty: 'Sin enfermedades registradas.' },
  { key: 'medications', title: 'Medicamentos', empty: 'Sin medicamentos registrados.' },
] as const;
type Category = typeof categories[number];
function entries(review: Review | null, key: Category['key']) {
  if (!review) return [];
  if (key === 'allergies') return review.allergies.map(x => ({ name: x.name, detail: [x.reaction, x.severity].filter(Boolean).join(' · '), active: x.isCurrentlyActive }));
  if (key === 'conditions') return review.conditions.map(x => ({ name: x.conditionName, detail: x.details, active: x.isCurrentlyActive }));
  return review.medications.map(x => ({ name: x.medicationName, detail: [x.dose, x.frequency, x.reason].filter(Boolean).join(' · '), active: x.isCurrentlyActive }));
}
function emptyText(review: Review | null, category: Category) {
  if (category.key === 'allergies' && review?.noKnownAllergies) return 'Sin alergias conocidas: confirmado por el paciente.';
  return entries(review, category.key).length ? 'Sin registros activos en esta revisión.' : category.empty;
}
export default function ClinicalMedicalAlerts({ review, loadHistory, onUpdate }: {
  review: Review | null; loadHistory: () => Promise<History>; onUpdate?: () => void;
}) {
  const id = useId(), live = useRef(true);
  const [open, setOpen] = useState(false), [history, setHistory] = useState<Review[] | null>(null);
  const [loading, setLoading] = useState(false), [error, setError] = useState('');
  useEffect(() => { live.current = true; return () => { live.current = false; }; }, []);
  async function toggle() {
    setOpen(!open);
    if (open || history || loading) return;
    setLoading(true); setError('');
    try {
      const result = await loadHistory();
      if (live.current) setHistory([...result.medicalReviews].sort((a,b) => Date.parse(b.reviewedAt)-Date.parse(a.reviewedAt)));
    } catch { if (live.current) setError('No se pudo cargar el historial. Cierra y vuelve a abrir para reintentar.'); }
    finally { if (live.current) setLoading(false); }
  }
  return <section className="card clinical-alerts" aria-label="Alertas y antecedentes actuales">
    <div className="clinical-alerts-heading"><div><h2>Información médica relevante</h2><p>Estado actual{review ? ` · Revisión ${new Date(review.reviewedAt).toLocaleDateString('es-BO')}` : ' · Pendiente de registrar'}</p></div>
      {onUpdate && <button type="button" className="secondary" onClick={onUpdate}>Revisar / agregar antecedentes</button>}
    </div>
    <div className="clinical-alerts-grid">{categories.map(category => {
      const current = entries(review, category.key).filter(x => x.active);
      return <section key={category.key} className={`clinical-alert-category clinical-alert-${category.key}`} aria-label={`${category.title} actuales`}><h3>{category.title}</h3>
        {current.length ? <ul className="clinical-alert-chips">{current.map((entry,i) => <li key={i}><strong>{entry.name}</strong>{entry.detail && <small>{entry.detail}</small>}</li>)}</ul> : <p>{emptyText(review, category)}</p>}
      </section>;
    })}</div>
    <button type="button" className="secondary clinical-alert-toggle" aria-expanded={open} aria-controls={id} onClick={()=>void toggle()}><span aria-hidden="true">{open?'▴':'▾'}</span> {open?'Ocultar':'Ver'} historial de antecedentes</button>
    <div id={id} hidden={!open} className="clinical-alert-history">
      {loading && <p role="status">Cargando antecedentes históricos…</p>}{error && <p role="alert">{error}</p>}
      {history && (history.length ? <div className="clinical-alerts-grid">{categories.map(category => <section key={category.key} aria-label={`Historial de ${category.title.toLowerCase()}`}><h3>{category.title} · Historial</h3><ol>{history.map(r => <li key={r.id}>
        <time dateTime={r.reviewedAt}>{new Date(r.reviewedAt).toLocaleString('es-BO')}</time><small>{r.stateReviewId?'Revisión sin cambios':'Registro / actualización'}</small>
        {category.key==='allergies' && r.noKnownAllergies && <p>Sin alergias conocidas: confirmado por el paciente.</p>}
        {entries(r,category.key).length ? <ul>{entries(r,category.key).map((x,i)=><li key={i}><strong>{x.name}</strong> · {x.active?'Activo':'Inactivo'}{x.detail && <span> · {x.detail}</span>}</li>)}</ul> : !(category.key==='allergies'&&r.noKnownAllergies) && <p>{category.empty} En esta fecha.</p>}
      </li>)}</ol></section>)}</div> : <p>No hay revisiones médicas registradas.</p>)}
    </div>
  </section>;
}
