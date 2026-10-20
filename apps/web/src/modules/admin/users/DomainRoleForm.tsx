import { useState, type FormEvent } from 'react';
import { api, type User } from '../../../services/api';
import { Fields, FormActions, Feedback } from '../shared';

export const domainRoles = [
  { code: 'DOCTOR', profileKey: 'hasDoctorProfile', label: 'Doctor', endpoint: 'doctor-profile', permission: 'doctors.create' },
  { code: 'DENTAL_ASSISTANT', profileKey: 'hasDentalAssistantProfile', label: 'Asistente dental', endpoint: 'dental-assistant-profile', permission: 'doctors.create' },
  { code: 'PATIENT', profileKey: 'hasPatientProfile', label: 'Paciente', endpoint: 'patient-profile', permission: 'patients.create' },
] as const;
export type DomainRole = typeof domainRoles[number];
export default function DomainRoleForm({ user, role, done, cancel }: { user: User; role: DomainRole; done: () => Promise<void>; cancel: () => void }) {
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const label = user.roles.some(r => r.code === role.code) ? `Completar perfil ${role.label}` : `Añadir rol ${role.label}`;
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const data = Object.fromEntries(new FormData(e.currentTarget)); setBusy(true); setError('');
    try { await api(`/api/admin/users/${user.id}/${role.endpoint}`, 'POST', data); await done(); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <section className="card"><h2>{label}</h2><p>{user.firstName} {user.lastName}. Se conservarán su identidad, cuenta y roles actuales.</p>
    <Feedback error={error} /><form className="editor" onSubmit={save}>
      {role.code === 'DOCTOR' && <Fields fields={[{ name: 'professionalLicense', label: 'Matrícula profesional', required: true, maxLength: 100 }, { name: 'photoUrl', label: 'Foto URL', type: 'url', maxLength: 1000 }, { name: 'bio', label: 'Biografía', type: 'textarea', maxLength: 4000 }]} />}
      {role.code === 'PATIENT' && <><Fields fields={[{ name: 'emergencyContactName', label: 'Contacto de emergencia', required: true, maxLength: 200 }, { name: 'emergencyContactPhone', label: 'Teléfono de emergencia', required: true, maxLength: 30 }]} />
        <label>Grupo sanguíneo<select name="bloodType" required defaultValue=""><option value="">Seleccionar</option>{['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'UNKNOWN'].map(value => <option key={value} value={value}>{value === 'UNKNOWN' ? 'Desconocido' : value}</option>)}</select></label>
        <Fields fields={[{ name: 'notes', label: 'Notas', type: 'textarea', maxLength: 4000 }]} /></>}
      <FormActions busy={busy} label={`Confirmar rol ${role.label}`} cancel={cancel} />
    </form></section>;
}
