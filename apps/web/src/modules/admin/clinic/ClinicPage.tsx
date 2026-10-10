import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, Fields, FormActions, PageTitle, readFields, useAction, usePermissions, type FieldSpec } from '../shared';
import type { Clinic } from '../types';
import ClinicLogoUploader from './ClinicLogoUploader';
import ClinicSocialNetworks from './ClinicSocialNetworks';

const fields: FieldSpec[] = [
  { name: 'name', label: 'Nombre', required: true, maxLength: 200 },
  { name: 'legalName', label: 'Razón social', maxLength: 250 },
  { name: 'description', label: 'Descripción', type: 'textarea' },
  { name: 'phone', label: 'Teléfono', maxLength: 30 }, { name: 'whatsapp', label: 'WhatsApp', maxLength: 30 },
  { name: 'email', label: 'Email', type: 'email', maxLength: 254 },
  { name: 'taxId', label: 'NIT', maxLength: 50 },
];
export default function ClinicPage() {
  const [clinic, setClinic] = useState<Clinic | null>(null); const action = useAction(); const can = usePermissions();
  useEffect(() => { api<Clinic>('/api/admin/clinic').then(setClinic).catch(e => action.setError(e.message)); }, []);
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const body = { ...readFields(event, fields), isActive: clinic!.isActive };
    void action.run(async () => { const saved = await api<Clinic>('/api/admin/clinic', 'PUT', body); setClinic(saved); }, 'Datos de la clínica actualizados.');
  }
  return <><PageTitle title="Clínica" description="Información institucional y canales de contacto." /><Feedback {...action} />
    {clinic ? <><ClinicLogoUploader logo={clinic.logo ?? null} onChanged={logo => setClinic(value => value ? { ...value, logo } : value)} />
      <section className="card"><form onSubmit={save} className="editor"><Fields fields={fields} value={clinic} disabled={!can('clinic.update') || action.busy} />{can('clinic.update') && <FormActions busy={action.busy} label="Guardar clínica" />}</form><p className="muted">Estos datos corresponden a toda la clínica.</p></section><ClinicSocialNetworks /></> : !action.error && <p role="status">Cargando clínica…</p>}</>;
}
