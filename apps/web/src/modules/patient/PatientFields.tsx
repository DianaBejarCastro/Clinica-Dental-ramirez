import IdentityDocumentFields, { documentData } from '../../components/IdentityDocumentFields';
import PasswordField from '../../components/PasswordField';
export function localToday() {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'America/La_Paz', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date());
  const part = (type: string) => parts.find(p => p.type === type)!.value;
  return `${part('year')}-${part('month')}-${part('day')}`;
}
const fields = [
  { name: 'firstName', label: 'Nombre', required: true, max: 100 },
  { name: 'lastName', label: 'Apellido', required: true, max: 100 },
  { name: 'documentNumber', label: 'Número de documento', required: true, max: 50 },
  { name: 'birthDate', label: 'Fecha de nacimiento', required: true, type: 'date' },
  { name: 'gender', label: 'Género', max: 50 },
  { name: 'phone', label: 'Teléfono', required: true, type: 'tel', max: 30 },
  { name: 'emergencyContactName', label: 'Contacto de emergencia', required: true, max: 200 },
  { name: 'emergencyContactPhone', label: 'Teléfono de emergencia', required: true, type: 'tel', max: 30 },
  { name: 'address', label: 'Dirección', max: 500 },
];
export const bloodTypes = ['A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-', 'UNKNOWN'];
export function PatientFields({ value = {}, admin = false, disabled = false }: { value?: object; admin?: boolean; disabled?: boolean }) {
  const data = value as Record<string, unknown>;
  return <>{fields.map(f => f.name === 'documentNumber' ? <IdentityDocumentFields key={f.name} value={data} disabled={disabled} /> : <label key={f.name}>{f.label}<input name={f.name} type={f.type ?? 'text'} required={f.required} maxLength={f.max}
    max={f.name === 'birthDate' ? localToday() : undefined} defaultValue={String(data[f.name] ?? '')} disabled={disabled} /></label>)}
    <label>Grupo sanguíneo<select name="bloodType" required defaultValue={String(data.bloodType ?? '')} disabled={disabled}>
      <option value="">Selecciona una opción</option>{bloodTypes.map(value => <option key={value} value={value}>{value === 'UNKNOWN' ? 'Desconocido' : value}</option>)}
    </select></label>
    {admin && <><label>Notas<textarea name="notes" maxLength={4000} rows={3} defaultValue={String(data.notes ?? '')} disabled={disabled} /></label></>}
  </>;
}
export function patientData(form: FormData, admin = false) {
  const values = Object.fromEntries(fields.map(f => [f.name, String(form.get(f.name) ?? '').trim() || null]));
  return { ...values, ...documentData(form), bloodType: String(form.get('bloodType') ?? ''), ...(admin ? { notes: String(form.get('notes') ?? '').trim() || null } : {}) };
}
export function patientError(form: FormData) {
  if (fields.some(f => f.required && !String(form.get(f.name) ?? '').trim())) return 'Completa los datos obligatorios del paciente.';
  if (String(form.get('birthDate')) > localToday()) return 'La fecha de nacimiento no puede ser futura.';
  if (!bloodTypes.includes(String(form.get('bloodType')))) return 'Selecciona un grupo sanguíneo o Desconocido.';
  return '';
}
export function PatientAccountFields({ publicRegistration = false, disabled = false }: { publicRegistration?: boolean; disabled?: boolean }) {
  return <><label>{publicRegistration ? 'Email' : 'Email de acceso'}<input name={publicRegistration ? 'email' : 'accountEmail'} type="email" required maxLength={254} autoComplete="email" disabled={disabled} /></label>
    <PasswordField label="Contraseña" name={publicRegistration ? 'password' : 'accountPassword'}  required minLength={12} maxLength={128} autoComplete="new-password" disabled={disabled} />
    <PasswordField label="Confirmar contraseña" name={publicRegistration ? 'confirmPassword' : 'accountConfirmPassword'}  required maxLength={128} autoComplete="new-password" disabled={disabled} /></>;
}
export function accountData(form: FormData) {
  const account = { email: String(form.get('accountEmail') ?? '').trim(), password: String(form.get('accountPassword') ?? ''), confirmPassword: String(form.get('accountConfirmPassword') ?? '') };
  if (account.password !== account.confirmPassword) throw new Error('Las contraseñas no coinciden.');
  if (account.password.length < 12 || account.password.length > 128) throw new Error('La contraseña debe tener entre 12 y 128 caracteres.');
  return account;
}
