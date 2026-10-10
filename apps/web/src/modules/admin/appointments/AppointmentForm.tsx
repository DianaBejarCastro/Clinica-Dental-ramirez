import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import type { Doctor } from '../types';
import { Feedback, useAction, usePermissions } from '../shared';
import { slotKey, timeLabel, type Appointment, type Availability, type Option, type Slot } from './types';

export default function AppointmentForm({ edit, patients, doctors, centers, specialties, saved, close }: {
  edit: Appointment | null; patients: Option[]; doctors: Doctor[]; centers: Option[]; specialties: Option[];
  saved: () => void; close: () => void;
}) {
  const can = usePermissions(); const action = useAction();
  const [patientId, setPatient] = useState(edit?.patientId ?? '');
  const [specialtyId, setSpecialty] = useState(edit?.specialtyId ?? '');
  const [doctorId, setDoctor] = useState(edit?.doctorId ?? '');
  const [centerId, setCenter] = useState(edit?.centerId ?? '');
  const [date, setDate] = useState(edit?.appointmentDate ?? '');
  const minutes = (value: string) => Number(value.slice(0, 2)) * 60 + Number(value.slice(3, 5));
  const [duration, setDuration] = useState(edit ? minutes(edit.endTime) - minutes(edit.startTime) : 30);
  const [selection, setSelection] = useState(''); const [slots, setSlots] = useState<Slot[]>([]);
  const [loading, setLoading] = useState(false); const [loadedKey, setLoadedKey] = useState('');
  const [slotError, setSlotError] = useState(''); const [revision, setRevision] = useState(0);
  const ready = !!doctorId && !!centerId && !!specialtyId && !!date && Number.isInteger(duration) && duration > 0 && duration <= 1440;
  const query = new URLSearchParams({ doctorId, centerId, specialtyId, date, durationMinutes: String(duration) }).toString();
  const url = `/api/admin/appointments/availability?${query}`;
  const key = ready ? query : '';
  const current = edit && !['CANCELLED', 'NO_SHOW'].includes(edit.status) && edit.doctorId === doctorId
    && edit.centerId === centerId && edit.specialtyId === specialtyId && edit.appointmentDate === date
    && minutes(edit.endTime) - minutes(edit.startTime) === duration ? edit : null;
  function clearSlots() { setSelection(''); setSlots([]); setLoadedKey(''); setSlotError(''); }
  useEffect(() => {
    let active = true; setSelection(''); setSlots([]); setLoadedKey(''); setSlotError('');
    if (!key) { setLoading(false); return; }
    setLoading(true);
    api<Availability>(url).then(value => { if (active) { setSlots(value.slots); setLoadedKey(key); } })
      .catch(error => { if (active) setSlotError(error.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [key, revision]);
  const selected = selection === 'current' ? current : slots.find(slot => slotKey(slot) === selection);
  const doctorOptions = doctors.filter(d => d.isActive && d.user.isActive && d.specialties.some(s => s.id === specialtyId && s.isActive));
  const doctor = doctors.find(d => d.id === doctorId);
  const centerOptions = centers.filter(c => c.isActive && doctor?.centers.some(a => a.id === c.id && a.isActive));
  const maySave = can(edit ? 'appointments.update' : 'appointments.create');
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!maySave || action.busy || loading || !selected || loadedKey !== key) return;
    const data = new FormData(event.currentTarget);
    const chosen = selected;
    void action.run(async () => {
      // Nunca confiar en una respuesta de disponibilidad anterior al envío.
      const fresh = await api<Availability>(url);
      if (selection === 'current' && current) {
        const latest = await api<Appointment>(`/api/admin/appointments/${current.id}`);
        if (latest.updatedAt !== current.updatedAt || latest.status !== current.status || slotKey(latest) !== slotKey(current)
          || latest.doctorId !== doctorId || latest.centerId !== centerId || latest.appointmentDate !== date)
          throw new Error('La cita cambió. Cierra el formulario y vuelve a editarla.');
      } else if (!fresh.slots.some(slot => slotKey(slot) === slotKey(chosen))) {
        clearSlots(); setRevision(v => v + 1);
        throw new Error('El horario seleccionado ya no está disponible. Selecciona otro.');
      }
      try {
        await api(edit ? `/api/admin/appointments/${edit.id}` : '/api/admin/appointments', edit ? 'PUT' : 'POST', {
          patientId, specialtyId, doctorId, centerId, appointmentDate: date,
          startTime: chosen.startTime, endTime: chosen.endTime,
          reason: String(data.get('reason') ?? '').trim() || null, notes: String(data.get('notes') ?? '').trim() || null,
        });
      } catch (error) { clearSlots(); setRevision(v => v + 1); throw error; }
      saved();
    });
  }
  return <section className="card"><h2>{edit ? 'Editar cita' : 'Nueva cita'}</h2>
    <Feedback {...action} /><Feedback error={slotError} />
    <form className="editor" onSubmit={save}>
      <label>Paciente<select required value={patientId} disabled={action.busy} onChange={e => setPatient(e.target.value)}>
        <option value="">Selecciona un paciente</option>{patients.filter(p => p.isActive).map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select></label>
      <label>Especialidad<select required value={specialtyId} disabled={action.busy} onChange={e => { clearSlots(); setSpecialty(e.target.value); setDoctor(''); setCenter(''); }}>
        <option value="">Selecciona una especialidad</option>{specialties.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
      </select></label>
      <label>Doctor<select required value={doctorId} disabled={action.busy || !specialtyId} onChange={e => { clearSlots(); setDoctor(e.target.value); setCenter(''); }}>
        <option value="">Selecciona un doctor</option>{doctorOptions.map(d => <option key={d.id} value={d.id}>{d.user.firstName} {d.user.lastName}</option>)}
      </select></label>
      <label>Sucursal<select required value={centerId} disabled={action.busy || !doctorId} onChange={e => { clearSlots(); setCenter(e.target.value); }}>
        <option value="">Selecciona una sucursal</option>{centerOptions.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
      </select></label>
      <label>Fecha<input type="date" required value={date} disabled={action.busy} onChange={e => { clearSlots(); setDate(e.target.value); }} /></label>
      <label>Duración (minutos)<input type="number" min={1} max={1440} step={1} required value={duration || ''} disabled={action.busy}
        onChange={e => { clearSlots(); setDuration(Number(e.target.value)); }} /></label>
      <label>Horario disponible<select required value={selection} disabled={action.busy || loading || !key || loadedKey !== key}
        onChange={e => setSelection(e.target.value)}>
        <option value="">Selecciona un horario</option>
        {current && loadedKey === key && <option value="current">{timeLabel(current.startTime)} – {timeLabel(current.endTime)} (reserva actual)</option>}
        {slots.map(slot => <option key={slotKey(slot)} value={slotKey(slot)}>{timeLabel(slot.startTime)} – {timeLabel(slot.endTime)}</option>)}
      </select></label>
      {loading && <p role="status">Consultando disponibilidad…</p>}
      {!loading && key && loadedKey === key && slots.length === 0 && <p>No hay horarios disponibles para una nueva reserva.</p>}
      {current && <p className="muted">La reserva actual pertenece a esta cita; puedes conservarla o elegir otro horario disponible.</p>}
      {slotError && <button type="button" disabled={action.busy} onClick={() => setRevision(v => v + 1)}>Reintentar disponibilidad</button>}
      <label>Motivo<input name="reason" maxLength={1000} defaultValue={edit?.reason ?? ''} disabled={action.busy} /></label>
      <label>Notas<textarea name="notes" maxLength={4000} defaultValue={edit?.notes ?? ''} disabled={action.busy} /></label>
      <div className="actions"><button disabled={!maySave || action.busy || loading || !selected || loadedKey !== key}>Guardar cita</button>
        <button type="button" className="secondary" disabled={action.busy} onClick={close}>Cerrar formulario</button></div>
    </form>
  </section>;
}
