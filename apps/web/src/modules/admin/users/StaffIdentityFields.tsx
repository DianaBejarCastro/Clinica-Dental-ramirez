import PasswordField from '../../../components/PasswordField';
import { Fields } from '../shared';
import { personalFields, accountFields } from '../medical-team/fields';

export default function StaffIdentityFields({ value = {}, creation = false, canLogin = true }: { value?: object; creation?: boolean; canLogin?: boolean }) {
  return <><Fields fields={personalFields} value={value} />
    <Fields fields={accountFields.map(f => ({ ...f, label: 'Email', required: canLogin }))} value={value} />
    {creation && <><PasswordField label="Contraseña" name="password"  autoComplete="new-password" required minLength={12} maxLength={128} />
      <PasswordField label="Confirmar contraseña" name="confirmPassword"  autoComplete="new-password" required maxLength={128} /></>}
  </>;
}
