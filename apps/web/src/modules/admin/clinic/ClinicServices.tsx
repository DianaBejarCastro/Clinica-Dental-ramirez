import { useEffect, useId, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, useAction, usePermissions } from '../shared';
import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { serviceVariants, type ClinicService } from '../../clinicServices';
import './clinic-services.css';

const root = '/api/admin/clinic/services';
export default function ClinicServices() {
  const [items, setItems] = useState<ClinicService[]>([]);
  const [specialties, setSpecialties] = useState<{ id: string; name: string }[]>([]);
  const [editor, setEditor] = useState<ClinicService | 'new' | null>(null);
  const [loading, setLoading] = useState(true);
  const action = useAction(), can = usePermissions(), title = useId();
  useEffect(() => {
    let live = true;
    Promise.all([api<ClinicService[]>(root), api<{ id: string; name: string }[]>(root + '/specialties')])
      .then(([services, options]) => { if (live) { setItems(services); setSpecialties(options); } })
      .catch(e => { if (live) action.setError(e.message); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, []);
  async function reload() { setItems(await api<ClinicService[]>(root)); }
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (action.busy || !can('clinic.update')) return;
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name')).trim();
    if (!name) { action.setError('Escribe el nombre del servicio.'); return; }
    const body = { name, shortDescription: String(form.get('shortDescription')).trim(), visualVariant: String(form.get('visualVariant')),
      displayOrder: Number(form.get('displayOrder')), isActive: form.has('isActive'), isPublic: form.has('isPublic'), specialtyId: form.get('specialtyId') || null };
    void action.run(async () => {
      const result = await api<ClinicService>(editor && editor !== 'new' ? `${root}/${editor.id}` : root, editor === 'new' ? 'POST' : 'PUT', body);
      setItems(current => [...current.filter(x => x.id !== result.id), result]); setEditor(null);
    }, 'Servicio guardado.');
  }
  const value = editor && editor !== 'new' ? editor : undefined;
  return <section className="card clinic-services" aria-labelledby={title}>
    <h2 id={title}>Servicios del inicio</h2>
    <p className="muted">Configura los servicios que se muestran en la sección pública «Nuestros servicios».</p>
    <Feedback {...action} />
    {can('clinic.update') && !editor && <button type="button" onClick={() => setEditor('new')}>+ Agregar servicio</button>}
    {editor && <form className="clinic-service-editor" onSubmit={save} key={value?.id ?? 'new'} aria-label={value ? 'Editar servicio' : 'Crear servicio'}>
      <fieldset disabled={action.busy}><legend>{value ? 'Editar servicio' : 'Crear servicio'}</legend>
        <label>Nombre<input name="name" required maxLength={120} defaultValue={value?.name} /></label>
        <label>Descripción corta<textarea name="shortDescription" maxLength={300} rows={3} defaultValue={value?.shortDescription} /></label>
        <label>Color / estilo<select name="visualVariant" defaultValue={value?.visualVariant ?? 'TEAL'}>{Object.entries(serviceVariants).map(([key, name]) => <option key={key} value={key}>{name}</option>)}</select></label>
        <label>Orden<input type="number" name="displayOrder" min={0} step={1} required defaultValue={value?.displayOrder ?? 0} /></label>
        <label>Especialidad relacionada<select name="specialtyId" defaultValue={value?.specialtyId ?? ''}><option value="">Sin relación (opcional)</option>{specialties.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label><input type="checkbox" name="isPublic" defaultChecked={value?.isPublic ?? true} /> Mostrar públicamente</label>
        <label><input type="checkbox" name="isActive" defaultChecked={value?.isActive ?? true} /> Activo</label>
      </fieldset>
      <div className="actions"><button disabled={action.busy} type="submit">Guardar servicio</button><button disabled={action.busy} type="button" className="secondary" onClick={() => setEditor(null)}>Cancelar</button></div>
    </form>}
    {loading ? <p role="status">Cargando servicios…</p> : !items.length && <p>No hay servicios configurados.</p>}
    <ul className="clinic-service-list">{[...items].sort((a, b) => a.displayOrder - b.displayOrder || a.id.localeCompare(b.id)).map(item => <li key={item.id}>
      <div><h3>{item.name}</h3><p>{item.shortDescription}</p><small>Orden: {item.displayOrder} · {serviceVariants[item.visualVariant]} · {item.isActive ? 'Activo' : 'Inactivo'} · {item.isPublic ? 'Público' : 'No público'}</small></div>
      {can('clinic.update') && <div className="actions"><button className="secondary" disabled={action.busy} onClick={() => setEditor(item)}>Editar</button>
        <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => { await api(`${root}/${item.id}/status`, 'PATCH', { isActive: !item.isActive }); await reload(); })}>{item.isActive ? 'Desactivar' : 'Activar'}</button>
        <ProtectedDeleteButton entity="clinic-services" id={item.id} label={item.name} onDeleted={reload} />
      </div>}
    </li>)}</ul>
  </section>;
}
