import { useEffect, useState } from 'react';
import { api } from '../../../services/api';
import ProfilePhotoField, { ProfileAvatar, persistProfilePhoto, useCanManagePhotos } from '../../../components/ProfilePhoto';
import { Feedback, useAction, usePermissions } from '../shared';
import './clinic-team.css';
interface Doctor { doctorProfileId: string; userId: string; firstName: string; lastName: string; professionalLicense: string; specialties: string[]; photoVersion: string | null }
interface Featured extends Doctor { id: string; displayOrder: number; isEligible: boolean }
const root = '/api/admin/clinic/team';
function DoctorIdentity({ doctor }: { doctor: Doctor }) {
  return <div className="clinic-team-identity">{doctor.photoVersion ? <ProfileAvatar userId={doctor.userId} firstName={doctor.firstName} lastName={doctor.lastName} /> : <span className="profile-avatar" aria-label="Sin foto">{doctor.firstName[0]}{doctor.lastName[0]}</span>}
    <div><h3>{doctor.firstName} {doctor.lastName}</h3><p>Matrícula: {doctor.professionalLicense}</p><small>{doctor.specialties.join(' · ')}</small></div></div>;
}
function PhotoEditor({ doctor, close, saved }: { doctor: Doctor; close: () => void; saved: () => Promise<void> }) {
  const [photo, setPhoto] = useState<Blob | null | undefined>(); const action = useAction();
  return <div className="clinic-team-photo"><ProfilePhotoField userId={doctor.userId} onChange={setPhoto} disabled={action.busy} /><Feedback {...action} />
    <div className="actions"><button disabled={action.busy || photo === undefined} onClick={() => void action.run(async () => {
      const error = await persistProfilePhoto(doctor.userId, photo); if (error) throw new Error('No se pudo guardar la foto. Inténtalo de nuevo.');
      await saved(); close();
    })}>Guardar foto</button><button className="secondary" disabled={action.busy} onClick={close}>Cancelar foto</button></div>
  </div>;
}
export default function ClinicTeam() {
  const [selected, setSelected] = useState<Featured[]>([]), [available, setAvailable] = useState<Doctor[]>([]);
  const [loading, setLoading] = useState(true), [choosing, setChoosing] = useState(false), [editingPhoto, setEditingPhoto] = useState<string | null>(null);
  const can = usePermissions(), canPhoto = useCanManagePhotos(), action = useAction();
  async function reload() { const [a, b] = await Promise.all([api<Featured[]>(root), api<Doctor[]>(root + '/available')]); setSelected(a); setAvailable(b); }
  useEffect(() => { let live = true;
    Promise.all([api<Featured[]>(root), api<Doctor[]>(root + '/available')]).then(([a, b]) => { if (live) { setSelected(a); setAvailable(b); } })
      .catch(e => { if (live) action.setError(e.message); }).finally(() => { if (live) setLoading(false); }); return () => { live = false; };
  }, []);
  return <section className="card clinic-team" aria-labelledby="clinic-team-title"><h2 id="clinic-team-title">Equipo del inicio</h2>
    <p className="muted">Selecciona los profesionales que se mostrarán en la sección pública «Nuestro equipo».</p><Feedback {...action} />
    {loading ? <p role="status">Cargando profesionales…</p> : <>
      {can('clinic.update') && <button type="button" onClick={() => setChoosing(!choosing)} aria-expanded={choosing}>+ Agregar profesional</button>}
      {choosing && <div className="clinic-team-options" aria-label="Profesionales disponibles">{!available.length && <p>No hay otros doctores activos asignados a esta clínica.</p>}
        {available.map(doctor => <article key={doctor.doctorProfileId}><DoctorIdentity doctor={doctor} /><button disabled={action.busy} aria-label={`Agregar a ${doctor.firstName} ${doctor.lastName}`} onClick={() => void action.run(async () => {
          await api(root, 'POST', { doctorProfileId: doctor.doctorProfileId, displayOrder: selected.length ? Math.max(...selected.map(x => x.displayOrder)) + 1 : 0 }); await reload();
        })}>Agregar al inicio</button></article>)}</div>}
      {!selected.length && !action.error && <p>No hay profesionales seleccionados para el inicio.</p>}
      <div className="clinic-team-selected">{selected.map(doctor => <article key={doctor.id}><DoctorIdentity doctor={doctor} />
        {!doctor.isEligible && <p role="status">No se muestra públicamente: revisa el estado del profesional y su asignación a la clínica.</p>}
        {!doctor.photoVersion && <p className="muted">Este profesional todavía no tiene foto de perfil. Añade una foto para presentarlo en el inicio.</p>}
        {can('clinic.update') && <div className="clinic-team-actions"><form onSubmit={event => { event.preventDefault(); const data = new FormData(event.currentTarget); void action.run(async () => { await api(`${root}/${doctor.id}/order`, 'PUT', { displayOrder: Number(data.get('order')) }); await reload(); }); }}>
          <label>Orden de {doctor.firstName}<input aria-label={`Orden de ${doctor.firstName} ${doctor.lastName}`} name="order" type="number" min={0} step={1} required defaultValue={doctor.displayOrder} /></label><button className="secondary" disabled={action.busy}>Guardar orden</button></form>
          {canPhoto && <button className="secondary" disabled={action.busy} onClick={() => setEditingPhoto(doctor.id)}>{doctor.photoVersion ? 'Cambiar foto' : 'Subir foto'}</button>}
          <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => { await api(`${root}/${doctor.id}`, 'DELETE'); await reload(); })}>Quitar del inicio</button>
        </div>}
        {editingPhoto === doctor.id && canPhoto && <PhotoEditor doctor={doctor} close={() => setEditingPhoto(null)} saved={reload} />}
      </article>)}</div>
    </>}
  </section>;
}
