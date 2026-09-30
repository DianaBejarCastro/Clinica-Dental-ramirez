import { useState, type FormEvent } from 'react';
import { login } from '../../services/api';
export default function LoginPage() {
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget); setBusy(true); setError('');
    try { await login(String(data.get('email')), String(data.get('password'))); }
    catch (e) { setError(e instanceof Error ? e.message : 'Error de conexión.'); } finally { setBusy(false); }
  }
  return <main className="login"><section className="login-brand"><span className="mark">R</span><p>CLÍNICA DENTAL RAMÍREZ</p><h1>Todo empieza<br />con una sonrisa.</h1><p>Un espacio para organizar nuestro trabajo y cuidar mejor.</p></section><section className="login-form"><p className="eyebrow">ACCESO AL EQUIPO</p><h2>Bienvenido de nuevo</h2><p className="muted">Ingresa con tu cuenta de la clínica.</p><form onSubmit={submit}><label>Email<input name="email" type="email" autoComplete="username" required maxLength={254} /></label><label>Contraseña<input name="password" type="password" autoComplete="current-password" required maxLength={128} /></label>{error && <p role="alert" className="error">{error}</p>}<button disabled={busy}>{busy ? 'Ingresando…' : 'Ingresar'}</button></form><small>Clínica Dental RAMÍREZ · Administración</small></section></main>;
}
