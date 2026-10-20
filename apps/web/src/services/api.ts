export interface CurrentUser { id: string; firstName: string; lastName: string; email: string; roles: string[]; permissions: string[] }
export interface Session { accessToken: string; expiresAt: string; user: CurrentUser }
export interface Role { id: string; code: string; name: string; description: string | null; isActive: boolean; isSystem: boolean; permissionIds: string[] }
export interface Permission { id: string; code: string; name: string; module: string }
export interface User { id: string; firstName: string; lastName: string; email: string | null; phone: string | null; ci?: string | null; birthDate?: string | null; gender?: string | null; address?: string | null; canLogin?: boolean; hasDoctorProfile?: boolean; hasPatientProfile?: boolean; hasDentalAssistantProfile?: boolean; isActive: boolean; roles: { id: string; code?: string; name: string }[] }
const base = import.meta.env.VITE_API_URL ?? 'http://localhost:5000';
export const apiUrl = (path: string) => base + path;
let session: Session | null = null;
let refreshing: Promise<Session> | null = null;
const listeners = new Set<() => void>();
export const getSession = () => session;
export const subscribe = (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener); }; };
export function setSession(value: Session | null) { session = value; listeners.forEach(fn => fn()); }
export class ApiError extends Error { constructor(public status: number, message: string, public details?: unknown) { super(message); } }
async function send<T>(path: string, method = 'GET', data?: unknown): Promise<T> {
  const response = await fetch(base + path, { method, credentials: 'include', headers: {
    ...(data instanceof FormData ? {} : { 'Content-Type': 'application/json' }), 'X-Clinic-Request': '1',
    ...(session ? { Authorization: `Bearer ${session.accessToken}` } : {}),
  }, body: data instanceof FormData ? data : data === undefined ? undefined : JSON.stringify(data) });
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    throw new ApiError(response.status, problem.title ?? problem.message ?? (response.status === 403 ? 'No tienes permiso para esta acción.' : 'No se pudo completar la solicitud.'), problem);
  }
  return response.status === 204 ? undefined as T : response.json();
}
export function refresh(): Promise<Session> {
  if (!refreshing) refreshing = send<Session>('/api/auth/refresh', 'POST')
    .then(value => { setSession(value); return value; })
    .catch(error => { setSession(null); throw error; }).finally(() => { refreshing = null; });
  return refreshing;
}
export async function login(email: string, password: string) { const value = await send<Session>('/api/auth/login', 'POST', { email, password }); setSession(value); }
export async function logout() {
  if (refreshing) await refreshing.catch(() => undefined);
  await send('/api/auth/logout', 'POST');
  setSession(null);
}
export async function api<T>(path: string, method = 'GET', data?: unknown): Promise<T> {
  const token = session?.accessToken;
  try { return await send<T>(path, method, data); }
  catch (error) {
    if (!(error instanceof ApiError) || error.status !== 401) throw error;
    if (!session || session.accessToken === token) await refresh();
    try { return await send<T>(path, method, data); }
    catch (retryError) { if (retryError instanceof ApiError && retryError.status === 401) setSession(null); throw retryError; }
  }
}
