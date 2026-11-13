export interface GoogleIdentityClient {
  initialize(options: { client_id: string; nonce: string; callback: (value: { credential?: string }) => void; auto_select: boolean }): void;
  renderButton(element: HTMLElement, options: { theme: string; size: string; text: string; width: number }): void;
}
let loading: Promise<GoogleIdentityClient> | undefined;
export function loadGoogleIdentity(): Promise<GoogleIdentityClient> {
  const client = () => (window as Window & { google?: { accounts?: { id?: GoogleIdentityClient } } }).google?.accounts?.id;
  if (client()) return Promise.resolve(client()!);
  if (!loading) loading = new Promise<GoogleIdentityClient>((resolve, reject) => {
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client'; script.async = true; script.defer = true;
    const timeout = window.setTimeout(() => { script.remove(); reject(new Error('Google no está disponible. Usa tu email y contraseña o vuelve a intentar.')); }, 15000);
    script.onload = () => { clearTimeout(timeout); client() ? resolve(client()!) : reject(new Error('No se pudo cargar Google.')); };
    script.onerror = () => { clearTimeout(timeout); script.remove(); reject(new Error('No se pudo cargar Google.')); };
    document.head.appendChild(script);
  }).catch(error => { loading = undefined; throw error; });
  return loading;
}

// Only an opaque doctor UUID may survive login. Never accept an arbitrary return URL.
export function bookingDoctorFromHash() {
  const query = window.location.hash.split('?')[1] ?? '';
  const values = new URLSearchParams(query);
  const id = values.get('bookingDoctor') ?? values.get('doctor');
  return id && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) ? id : null;
}
export function patientDestination(incomplete = false, doctor = bookingDoctorFromHash()) {
  return incomplete ? `/patient/complete-profile${doctor ? `?bookingDoctor=${doctor}` : ''}`
    : doctor ? `/patient/book?doctor=${doctor}` : '/patient';
}
