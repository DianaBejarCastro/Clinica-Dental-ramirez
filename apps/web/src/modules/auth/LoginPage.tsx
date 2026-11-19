import LoginDentalVisual from './LoginDentalVisual';
import GoogleSignIn from './GoogleSignIn';
import './login.css';
import './login-reference.css';
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
  return <main className="login premium-login dental-login">
    <section className="login-card" aria-labelledby="login-title">
      <header className="login-heading">
        <BackLink href="#/inicio">Volver al inicio</BackLink>
        <ClinicLogo />
        <h1 id="login-title">Iniciar <span>sesión</span></h1>
        <p>Accede al sistema de la<br />Clínica Dental Ramírez</p>
      </header>
      <div className="login-illustration"><LoginDentalVisual /></div>
      <section className="login-form" aria-label="Acceso al sistema">
        {registered && <p role="status">Cuenta creada correctamente. Ya puedes iniciar sesión.</p>}
        <form onSubmit={submit}>
          <label className="login-email"><span className="login-sr-only">Email</span>
            <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 6 9 7 9-7"/></svg>
            <input name="email" type="email" placeholder="Correo electrónico" autoComplete="username" required maxLength={254} />
          </label>
          <div className="login-password">
            <svg className="login-lock" viewBox="0 0 24 24" aria-hidden="true"><rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3m-4 4v3"/></svg>
            <PasswordField label="Contraseña" name="password" placeholder="Contraseña" autoComplete="current-password" required maxLength={128} />
          </div>
          {error && <p role="alert" className="error">{error}</p>}
          <button className="login-submit" disabled={busy}>{busy ? 'Ingresando…' : 'Iniciar sesión'}<span aria-hidden="true">→</span></button>
        </form>
        <GoogleSignIn separator="o" />
        <p className="login-registration">¿No tienes una cuenta? <a href="#/register">Regístrate</a></p>
      </section>
    </section>
  </main>;
}
