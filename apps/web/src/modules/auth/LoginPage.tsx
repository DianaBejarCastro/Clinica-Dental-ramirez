import './login.css';
import BackLink from '../../components/BackLink';
import PasswordField from '../../components/PasswordField';
import ClinicLogo from '../admin/clinic/ClinicLogo';
import { useEffect, useState, type FormEvent } from 'react';
import { getSession, login } from '../../services/api';
import { registrationCompleted, clearRegistrationNotice } from './PatientRegisterPage';
export default function LoginPage() {
  const [registered] = useState(registrationCompleted);
  useEffect(() => { clearRegistrationNotice(); }, []);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget); setBusy(true); setError('');
    try { await login(String(data.get('email')), String(data.get('password'))); if (getSession()?.user.roles.includes('DOCTOR')) window.location.hash = '/doctor/appointments'; else if (getSession()?.user.roles.includes('CASHIER')) window.location.hash = '/cashier/payments'; }
    catch (e) { setError(e instanceof Error ? e.message : 'Error de conexión.'); } finally { setBusy(false); }
  }
  return <main className="login premium-login"><section className="login-brand"><ClinicLogo /><p>CLÍNICA DENTAL RAMÍREZ</p><h1>Todo empieza<br />con una sonrisa.</h1><p>Un espacio para organizar nuestro trabajo y cuidar mejor.</p></section><section className="login-form"><BackLink href="#/inicio">Volver al inicio</BackLink><p className="eyebrow">ACCESO AL EQUIPO</p><h2>Bienvenido de nuevo</h2><p className="muted">Ingresa con tu cuenta de la clínica.</p><>{registered && <p role="status">Cuenta creada correctamente. Ya puedes iniciar sesión.</p>}</><form onSubmit={submit}><label>Email<input name="email" type="email" autoComplete="username" required maxLength={254} /></label><PasswordField label="Contraseña" name="password" autoComplete="current-password" required maxLength={128} />{error && <p role="alert" className="error">{error}</p>}<button disabled={busy}>{busy ? 'Ingresando…' : 'Ingresar'}</button></form><p className="muted">¿No tienes una cuenta? <a href="#/register">Regístrate</a></p><small>Clínica Dental RAMÍREZ · Administración</small></section></main>;
}
