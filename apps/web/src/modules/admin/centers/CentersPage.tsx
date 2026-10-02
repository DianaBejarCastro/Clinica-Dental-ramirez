import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, Fields, FormActions, PageTitle, readFields, StateBadge, useAction, usePermissions, type FieldSpec } from '../shared';
import type { Center } from '../types';
const fields: FieldSpec[] = [
  { name: 'name', label: 'Nombre', required: true, maxLength: 200 }, { name: 'code', label: 'Código', required: true, maxLength: 80 },
  { name: 'description', label: 'Descripción', type: 'textarea' }, { name: 'address', label: 'Dirección', required: true, maxLength: 500 },
  { name: 'city', label: 'Ciudad', maxLength: 100 }, { name: 'department', label: 'Departamento', maxLength: 100 }, { name: 'country', label: 'País', required: true, maxLength: 100 },
  { name: 'phone', label: 'Teléfono', maxLength: 30 }, { name: 'whatsapp', label: 'WhatsApp', maxLength: 30 }, { name: 'email', label: 'Email', type: 'email', maxLength: 254 },
  { name: 'googleMapsUrl', label: 'Google Maps URL', type: 'url', maxLength: 2048 }, { name: 'latitude', label: 'Latitud', type: 'number', min: -90, max: 90, step: 'any' }, { name: 'longitude', label: 'Longitud', type: 'number', min: -180, max: 180, step: 'any' },
];
export default function CentersPage() {
  const [items, setItems] = useState<Center[]>([]); const [loaded, setLoaded] = useState(false); const [editing, setEditing] = useState<Center | 'new' | null>(null); const action = useAction(); const can = usePermissions();
  async function load() { setItems(await api<Center[]>('/api/admin/centers')); setLoaded(true); }
  useEffect(() => { load().catch(e => action.setError(e.message)); }, []);
  const edit = editing && editing !== 'new' ? editing : null;
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget); const body = { ...readFields(event, fields), isMain: data.has('isMain'), isActive: can('centers.disable') ? data.has('isActive') : edit?.isActive ?? true };
    void action.run(async () => { await api(edit ? `/api/admin/centers/${edit.id}` : '/api/admin/centers', edit ? 'PUT' : 'POST', body); setEditing(null); await load(); }, 'Sucursal guardada.');
  }
  return <><PageTitle title="Sucursales" description="Sedes de atención de Clínica Dental RAMÍREZ.">{can('centers.create') && <button onClick={() => setEditing('new')}>Crear sucursal</button>}</PageTitle><Feedback {...action} />
    {editing && <section className="card"><h2>{edit ? 'Editar sucursal' : 'Nueva sucursal'}</h2><form className="editor" key={edit?.id ?? 'new'} onSubmit={save}><Fields fields={fields} value={edit ?? { country: 'Bolivia' }} /><label className="check"><input type="checkbox" name="isMain" defaultChecked={edit?.isMain ?? false} />Sucursal principal</label>{can('centers.disable') && <label className="check"><input type="checkbox" name="isActive" defaultChecked={edit?.isActive ?? true} />Sucursal activa</label>}<p className="muted full-width">Solo puede existir una sucursal principal activa. Las coordenadas son opcionales.</p><FormActions busy={action.busy} label="Guardar sucursal" cancel={() => setEditing(null)} /></form></section>}
    <section className="card table-card">{!loaded ? !action.error && <p role="status">Cargando sucursales…</p> : <><table><thead><tr><th>Nombre / Código</th><th>Dirección</th><th>Teléfono</th><th>Google Maps</th><th>Principal</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>{items.map(center => <tr key={center.id}><td><strong>{center.name}</strong><small>{center.code}</small></td><td>{center.address}</td><td>{center.phone ?? '—'}</td><td>{center.googleMapsUrl && /^https?:\/\//i.test(center.googleMapsUrl) ? <a href={center.googleMapsUrl} target="_blank" rel="noreferrer">Ver mapa</a> : '—'}</td><td>{center.isMain ? 'Principal' : '—'}</td><td><StateBadge active={center.isActive} /></td><td><div className="actions">{can('centers.update') && <button className="secondary" onClick={() => setEditing(center)}>Editar</button>}{can('centers.disable') && <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => { await api(`/api/admin/centers/${center.id}/status`, 'PATCH', { isActive: !center.isActive }); await load(); }, 'Estado de sucursal actualizado.')}>{center.isActive ? 'Desactivar' : 'Activar'}</button>}</div></td></tr>)}</tbody></table>{items.length === 0 && <p>No hay sucursales. Crea la primera sede de atención.</p>}</>}</section></>;
}
