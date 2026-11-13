import { useEffect, useState, type FormEvent } from 'react';
import { api, getSession, setSession, type CurrentUser } from '../../services/api';
import { PatientFields, patientData } from '../patient/PatientFields';
import { normalizedPhone, phoneError } from '../../components/phoneRules';
import { patientDestination } from './googleIdentity';
import './patient-register.css';

export default function CompletePatientProfile() {
  const [value, setValue] = useState<Record<string, unknown> | null>(null);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => { let active = true; api<Record<string, unknown>>('/api/auth/patient-profile').then(x => { if (active) setValue(x); }).catch(e => { if (active) setError(e.message); }); return () => { active = false; }; }, []);
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (!value || busy) return;
    const form = new FormData(e.currentTarget); const parsed = patientData(form);
    const data = { ...value };
    for (const [key, item] of Object.entries(parsed)) if (form.has(key)) data[key] = item;
    if (form.has('documentNumber')) { data.documentType = parsed.documentType; data.documentCountryCode = parsed.documentCountryCode; }
    for (const name of ['phone', 'emergencyContactPhone']) if (form.has(name)) {
      const country = String(form.get(`${name}Country`)); const number = String(form.get(name));
      const invalid = phoneError(country, number); if (invalid) { setError(invalid); return; }
      data[name] = normalizedPhone(country, number);
    }
    setBusy(true); setError('');
    try {
      const user = await api<CurrentUser>('/api/auth/patient-profile', 'PUT', data);
      const destination = patientDestination(false);
      const session = getSession(); if (session) setSession({ ...session, user });
      window.location.hash = destination;
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo guardar el perfil.'); }
    finally { setBusy(false); }
  }
  return <main className="complete-patient-profile patient-register"><h1>Completar mi perfil</h1><p>Completa los datos que faltan para acceder a tus citas. No necesitas crear una contraseña.</p>
    {error && <p role="alert">{error}</p>}
    {!value ? <p role="status">Cargando perfil…</p> : <form onSubmit={submit}><fieldset disabled={busy} className="registration-fields"><PatientFields value={value} publicRegistration missingOnly /></fieldset><button disabled={busy}>{busy ? 'Guardando…' : 'Guardar y continuar'}</button></form>}
  </main>;
}
