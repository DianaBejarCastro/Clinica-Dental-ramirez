import { useState, type FormEvent } from 'react';
import PhoneInput from '../../../components/PhoneInput';
import { parsePhone, phoneError, normalizedPhone } from '../../../components/phoneRules';
import { api } from '../../../services/api';
import { Feedback, FormActions, useAction } from '../shared';
import type { Clinic } from '../types';

export default function ClinicWhatsapp({ clinic, onSaved, disabled }: { clinic: Clinic; onSaved: (clinic: Clinic) => void; disabled: boolean }) {
  const [phone, setPhone] = useState(() => parsePhone(clinic.whatsapp ?? ''));
  const action = useAction();
  function save(event: FormEvent) {
    event.preventDefault();
    const error = phoneError(phone.country, phone.nationalNumber, false);
    if (error) { action.setError(error); return; }
    void action.run(async () => {
      const saved = await api<Clinic>('/api/admin/clinic', 'PUT', { ...clinic, whatsapp: normalizedPhone(phone.country, phone.nationalNumber) });
      onSaved(saved);
    }, 'WhatsApp de contacto actualizado.');
  }
  return <section className="card" aria-labelledby="clinic-whatsapp-title">
    <h2 id="clinic-whatsapp-title">WhatsApp de contacto</h2>
    <p>Configura el número utilizado por el botón de WhatsApp de la página pública.</p>
    <Feedback {...action} />
    <form onSubmit={save}>
      <PhoneInput name="whatsapp" label="Número de WhatsApp" defaultValue={clinic.whatsapp ?? ''} disabled={disabled || action.busy} onChange={setPhone} />
      <p className="muted">Deja el número vacío para ocultar el botón público.</p>
      {!disabled && <FormActions busy={action.busy} label="Guardar WhatsApp" />}
    </form>
  </section>;
}
