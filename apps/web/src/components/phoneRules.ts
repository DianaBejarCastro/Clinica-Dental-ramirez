export const countryPhoneRules = [
  { countryCode: 'BO', dialCode: '+591', label: 'Bolivia', minLength: 8, maxLength: 8 },
  { countryCode: 'PE', dialCode: '+51', label: 'Perú', minLength: 9, maxLength: 9 },
  { countryCode: 'CL', dialCode: '+56', label: 'Chile', minLength: 9, maxLength: 9 },
  { countryCode: 'AR', dialCode: '+54', label: 'Argentina', minLength: 10, maxLength: 11 },
  { countryCode: 'BR', dialCode: '+55', label: 'Brasil', minLength: 10, maxLength: 11 },
];
export function phoneRule(country: string) { return countryPhoneRules.find(r => r.countryCode === country); }
export function phoneError(country: string, number: string, required = true) {
  if (!number && !required) return '';
  const rule = phoneRule(country);
  if (!rule) return 'Selecciona un país válido.';
  return /^[0-9]+$/.test(number) && number.length >= rule.minLength && number.length <= rule.maxLength ? '' :
    `El número para ${rule.label} debe tener ${rule.minLength === rule.maxLength ? rule.minLength : `${rule.minLength} a ${rule.maxLength}`} dígitos.`;
}
export function parsePhone(value: string) {
  const rule = countryPhoneRules.find(r => value.startsWith(r.dialCode));
  return { country: rule?.countryCode ?? 'BO', nationalNumber: rule ? value.slice(rule.dialCode.length) : value };
}
export function normalizedPhone(country: string, number: string) { return number ? `${phoneRule(country)?.dialCode ?? ''}${number}` : ''; }
