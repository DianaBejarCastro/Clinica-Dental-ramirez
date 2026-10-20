import { localToday } from '../patient/PatientFields';
import { useState, type FormEvent, type ReactNode } from 'react';
import { useAuth } from '../auth/AuthProvider';

export function usePermissions() {
  const { session } = useAuth();
  return (permission: string) => session?.user.permissions.includes(permission) ?? false;
}
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  async function run(work: () => Promise<unknown>, message = 'Cambios guardados.') {
    setBusy(true); setError(''); setSuccess('');
    try { await work(); setSuccess(message); } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return { busy, error, success, setError, run };
}
export function Feedback({ error, success }: { error?: string; success?: string }) {
  return <>{error && <p role="alert" className="error">{error}</p>}{success && <p role="status" className="success">{success}</p>}</>;
}
export function PageTitle({ title, description, children }: { title: string; description: string; children?: ReactNode }) {
  return <div className="title-row"><div><h1>{title}</h1><p className="muted">{description}</p></div>{children}</div>;
}
export function StateBadge({ active }: { active: boolean }) {
  return <span className={`badge ${active ? 'active' : 'inactive'}`}>{active ? 'Activo' : 'Inactivo'}</span>;
}
export interface FieldSpec { name: string; label: string; type?: string; required?: boolean; maxLength?: number; min?: number; max?: number; step?: string }
export function Fields({ fields, value = {}, disabled = false }: { fields: FieldSpec[]; value?: object; disabled?: boolean }) {
  const values = value as Record<string, unknown>;
  return <>{fields.map(field => <label key={field.name}>{field.label}{field.type === 'textarea' ? <textarea name={field.name} defaultValue={String(values[field.name] ?? '')} maxLength={field.maxLength ?? 2000} rows={3} disabled={disabled} required={field.required} /> : <input name={field.name} type={field.type ?? 'text'} defaultValue={String(values[field.name] ?? '')} required={field.required} maxLength={field.maxLength ?? 250} min={field.min} max={field.name === 'birthDate' ? localToday() : field.max} step={field.step} disabled={disabled} />}</label>)}</>;
}
export function readFields(event: FormEvent<HTMLFormElement>, fields: FieldSpec[]) {
  const data = new FormData(event.currentTarget);
  return Object.fromEntries(fields.map(field => {
    const value = String(data.get(field.name) ?? '').trim();
    return [field.name, value ? field.type === 'number' ? Number(value) : value : field.required ? '' : null];
  }));
}
export function FormActions({ busy, label, cancel }: { busy: boolean; label: string; cancel?: () => void }) {
  return <div className="actions"><button disabled={busy}>{busy ? 'Guardando…' : label}</button>{cancel && <button type="button" className="secondary" disabled={busy} onClick={cancel}>Cancelar</button>}</div>;
}
export function toggleId(ids: string[], id: string, checked: boolean) { return checked ? [...ids, id] : ids.filter(item => item !== id); }
