import { beforeEach, afterEach, it, expect, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import GoogleSignIn from '../modules/auth/GoogleSignIn';
import LoginPage from '../modules/auth/LoginPage';
import CompletePatientProfile from '../modules/auth/CompletePatientProfile';
import { bookingDoctorFromHash, patientDestination } from '../modules/auth/googleIdentity';
import { getSession, setSession } from '../services/api';
const gis = vi.hoisted(() => ({ callback: undefined as undefined | ((v: { credential?: string }) => void), initialize: vi.fn(), render: vi.fn() }));
vi.mock('../modules/auth/googleIdentity', async importOriginal => ({
  ...await importOriginal<typeof import('../modules/auth/googleIdentity')>(),
  loadGoogleIdentity: async () => ({ initialize: (options: { callback: typeof gis.callback }) => { gis.callback = options.callback; gis.initialize(options); }, renderButton: (node: HTMLElement) => { const button = document.createElement('button'); button.textContent = 'Continue with Google'; button.type = 'button'; node.append(button); gis.render(); } }),
}));
let incomplete = false, denied = false;
const doctor = '12345678-1234-1234-1234-123456789abc';
const payloads: Record<string, unknown>[] = [];
const user = () => ({ id: 'patient', firstName: 'Ana', lastName: 'Pérez', email: 'ana@example.test', roles: ['PATIENT'], permissions: [], requiresProfileCompletion: incomplete });
beforeEach(() => {
  incomplete = false; denied = false; payloads.length = 0; gis.callback = undefined; setSession(null); window.location.hash = '/login'; vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client');
  vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url.endsWith('/google/challenge')) return Response.json({ nonce: 'server-nonce' });
    if (url.endsWith('/google')) { payloads.push(JSON.parse(String(init?.body))); return denied ? Response.json({ title: 'Google sign-in is available only to patients. Please sign in with your email and password.' }, { status: 403 }) : Response.json({ accessToken: 'local-token', expiresAt: '2099-01-01', user: user() }); }
    if (url.endsWith('/patient-profile')) {
      if (init?.method === 'PUT') { payloads.push(JSON.parse(String(init.body))); incomplete = false; return Response.json(user()); }
      return Response.json({ firstName: 'Ana', lastName: 'Pérez', documentType: 'CI', documentNumber: '123', documentCountryCode: 'BO', birthDate: '1990-01-01', phone: '+59165700344', emergencyContactPhone: '', bloodType: 'UNKNOWN', gender: 'F', address: 'Calle' });
    }
    return Response.json({});
  }));
});
afterEach(() => { vi.unstubAllEnvs(); vi.unstubAllGlobals(); setSession(null); });
async function credential(value: { credential?: string } = { credential: 'google-id-token' }) { await waitFor(() => expect(gis.callback).toBeDefined()); await act(async () => { await gis.callback!(value); }); }
it('renders official GIS alongside unchanged email/password login', async () => {
  render(<LoginPage />); expect(screen.getByLabelText('Email')).toBeTruthy(); expect(screen.getByLabelText('Contraseña')).toBeTruthy(); expect(screen.getByRole('button', { name: 'Ingresar' })).toBeTruthy();
  expect(await screen.findByRole('button', { name: 'Continue with Google' })).toBeTruthy(); expect(gis.initialize).toHaveBeenCalledWith(expect.objectContaining({ client_id: 'test-client', nonce: 'server-nonce', auto_select: false }));
});
it('uses local session and opens existing patient portal', async () => { render(<GoogleSignIn />); await credential(); expect(getSession()?.accessToken).toBe('local-token'); expect(window.location.hash).toBe('#/patient'); expect(payloads[0]).toEqual({ credential: 'google-id-token' }); });
it('new patient goes to profile completion preserving selected doctor', async () => { incomplete = true; window.location.hash = '/login?bookingDoctor=' + doctor; render(<GoogleSignIn />); await credential(); expect(window.location.hash).toBe('#/patient/complete-profile?bookingDoctor=' + doctor); });
it('complete patient returns directly to booking', async () => { window.location.hash = '/login?bookingDoctor=' + doctor; render(<GoogleSignIn />); await credential(); expect(window.location.hash).toBe('#/patient/book?doctor=' + doctor); });
it('internal account rejection preserves local login and displays server explanation', async () => { denied = true; render(<LoginPage />); await credential(); expect((await screen.findByRole('alert')).textContent).toContain('only to patients'); expect(getSession()).toBeNull(); expect(screen.getByRole('button', { name: 'Ingresar' })).toBeTruthy(); });
it('cancel without a credential neither logs in nor reports a dramatic error', async () => { render(<GoogleSignIn />); await credential({}); expect(getSession()).toBeNull(); expect(payloads).toHaveLength(0); expect(screen.queryByRole('alert')).toBeNull(); });
it('missing configuration keeps password login available', () => { vi.stubEnv('VITE_GOOGLE_CLIENT_ID', ''); render(<LoginPage />); expect(screen.getByRole('button', { name: /no configurado/ }).hasAttribute('disabled')).toBe(true); expect(screen.getByRole('button', { name: 'Ingresar' })).toBeTruthy(); });
it('rejects arbitrary redirect destinations', () => { window.location.hash = '/login?returnUrl=https://evil.test&bookingDoctor=//evil.test'; expect(bookingDoctorFromHash()).toBeNull(); expect(patientDestination()).toBe('/patient'); });
it('completion reuses phone component, asks only missing data and returns to booking', async () => {
  incomplete = true; setSession({ accessToken: 'local-token', expiresAt: '2099-01-01', user: user() }); window.location.hash = '/patient/complete-profile?bookingDoctor=' + doctor;
  render(<CompletePatientProfile />); const phone = await screen.findByLabelText('Emergency Contact Phone Number');
  expect(phone.getAttribute('inputmode')).toBe('numeric'); expect(screen.queryByLabelText('Nombre')).toBeNull(); expect(screen.queryByLabelText('Contraseña')).toBeNull(); expect(screen.queryByLabelText('Email')).toBeNull();
  fireEvent.change(phone, { target: { value: '65700345' } }); fireEvent.click(screen.getByRole('button', { name: 'Guardar y continuar' }));
  await waitFor(() => expect(window.location.hash).toBe('#/patient/book?doctor=' + doctor)); expect(payloads[0].emergencyContactPhone).toBe('+59165700345'); expect(payloads[0].phone).toBe('+59165700344'); expect(getSession()?.user.requiresProfileCompletion).toBe(false);
});
