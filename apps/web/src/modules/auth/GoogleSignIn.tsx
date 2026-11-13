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
    {clientId ? <div ref={root} aria-busy={busy} /> : <button type="button" disabled>Continuar con Google (no configurado)</button>}
    <small>Disponible exclusivamente para pacientes.</small>
    {busy && <p role="status">Verificando tu cuenta…</p>}
    {error && <p role="alert">{error}</p>}
  </section>;
}
