import { useEffect, useRef, useState } from 'react';
import { googleChallenge, googleLogin } from '../../services/api';
import { loadGoogleIdentity, bookingDoctorFromHash, patientDestination } from './googleIdentity';

export default function GoogleSignIn() {
  const root = useRef<HTMLDivElement>(null);
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID;
  useEffect(() => {
    if (!clientId) return;
    let active = true; let submitting = false;
    Promise.all([loadGoogleIdentity(), googleChallenge()]).then(([google, challenge]) => {
      if (!active || !root.current) return;
      google.initialize({ client_id: clientId, nonce: challenge.nonce, auto_select: false, callback: async ({ credential }) => {
        if (!active || !credential || submitting) return;
        submitting = true; setBusy(true); setError('');
        const doctor = bookingDoctorFromHash();
        try { const session = await googleLogin(credential); window.location.hash = patientDestination(session.user.requiresProfileCompletion, doctor); }
        catch (e) { if (active) { setError(e instanceof Error ? e.message : 'Your Google account could not be verified.'); setAttempt(x => x + 1); } }
        finally { submitting = false; if (active) setBusy(false); }
      } });
      root.current.replaceChildren();
      google.renderButton(root.current, { theme: 'outline', size: 'large', text: 'continue_with', width: Math.min(360, root.current.clientWidth || 280) });
    }).catch(e => { if (active) setError(e instanceof Error ? e.message : 'No se pudo cargar Google.'); });
    return () => { active = false; };
  }, [clientId, attempt]);
  return <section className="google-sign-in" aria-label="Acceso de pacientes con Google">
    <p className="google-separator">o continúa con</p>
    {clientId ? <div ref={root} aria-busy={busy} /> : <button type="button" className="google-coming-soon" disabled>
      <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
        <path fill="#4285F4" d="M21.6 12.23c0-.71-.06-1.39-.18-2.05H12v3.88h5.38a4.6 4.6 0 0 1-2 3.02v2.51h3.24c1.89-1.74 2.98-4.31 2.98-7.36Z" />
        <path fill="#34A853" d="M12 22c2.7 0 4.96-.9 6.62-2.41l-3.24-2.51c-.9.6-2.05.96-3.38.96-2.6 0-4.81-1.76-5.6-4.12H3.06v2.59A10 10 0 0 0 12 22Z" />
        <path fill="#FBBC05" d="M6.4 13.92a6 6 0 0 1 0-3.84V7.49H3.06a10 10 0 0 0 0 9.02l3.34-2.59Z" />
        <path fill="#EA4335" d="M12 5.96c1.47 0 2.79.51 3.83 1.51l2.87-2.87A9.6 9.6 0 0 0 12 2a10 10 0 0 0-8.94 5.49l3.34 2.59C7.19 7.72 9.4 5.96 12 5.96Z" />
      </svg><span>Continuar con Google (próximamente)</span>
    </button>}
    <small>Disponible exclusivamente para pacientes.</small>
    {busy && <p role="status">Verificando tu cuenta…</p>}
    {error && <p role="alert">{error}</p>}
  </section>;
}
