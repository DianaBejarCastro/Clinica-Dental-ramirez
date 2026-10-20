import BackLink from '../../components/BackLink';
import { PatientFields, PatientAccountFields, patientData, patientError } from '../patient/PatientFields';
import { useState, type FormEvent } from 'react';
import { apiUrl } from '../../services/api';
import ClinicLogo from '../admin/clinic/ClinicLogo';
import './patient-register.css';
export let registrationCompleted = false;
export function clearRegistrationNotice() { registrationCompleted = false; }

export default function PatientRegisterPage() {
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (busy) return;
    const form = new FormData(event.currentTarget); setError('');
    const password = String(form.get('password')); const confirmPassword = String(form.get('confirmPassword'));
    if (password.length < 12 || password.length > 128) { setError('La contraseña debe tener entre 12 y 128 caracteres.'); return; }
    if (password !== confirmPassword) { setError('Las contraseñas no coinciden.'); return; }
    const text = (key: string) => String(form.get(key) ?? '').trim();
    if (!text('firstName') || !text('lastName')) { setError('Nombre y apellido son obligatorios.'); return; }
    const clinicalError = patientError(form); if (clinicalError) { setError(clinicalError); return; }
    setBusy(true);
    try {
      const response = await fetch(apiUrl('/api/auth/register/patient'), { method: 'POST', credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-Clinic-Request': '1' },
        body: JSON.stringify({ ...patientData(form), email: text('email'), password, confirmPassword }) });
      if (!response.ok) {
        const body = await response.json().catch(() => ({}));
        setError(response.status === 429 ? 'Demasiados intentos. Espera un momento antes de volver a intentar.' :
          response.status === 400 ? 'Revisa los campos y utiliza una contraseña de 12 a 128 caracteres.' :
          response.status === 409 ? body.title ?? 'El correo o CI ya está registrado.' : 'No se pudo crear la cuenta. Inténtalo más tarde.'); return;
      }
      registrationCompleted = true; window.location.hash = '/login';
    } catch { setError('No se pudo conectar. Inténtalo nuevamente.'); }
    finally { setBusy(false); }
  }
  return <main className="login patient-register"><section className="login-brand"><ClinicLogo /><p>CLÍNICA DENTAL RAMÍREZ</p><h1>Tu sonrisa,<br />más cerca.</h1><p>Crea tu cuenta de paciente.</p></section>
    <section className="login-form"><BackLink href="#/inicio">Volver al inicio</BackLink><h2>Crear cuenta de paciente</h2>
      <form onSubmit={submit}><fieldset disabled={busy} className="registration-fields">
        <PatientFields />
        <PatientAccountFields publicRegistration />
      </fieldset><p className="muted">Contraseña de 12 a 128 caracteres.</p>
      {error && <p role="alert" className="error">{error}</p>}<button disabled={busy}>{busy ? 'Creando cuenta…' : 'Crear cuenta'}</button></form>
      <p>Ya tengo una cuenta · <a href="#/login">Iniciar sesión</a></p>
    </section></main>;
}
