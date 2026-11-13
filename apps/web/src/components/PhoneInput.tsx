import { useId, useState } from 'react';
import { countryPhoneRules, parsePhone, phoneError, normalizedPhone } from './phoneRules';
import './phone-input.css';
export default function PhoneInput({ name, label, defaultValue = '', required = false, disabled = false, onChange }: {
  name: string; label: string; defaultValue?: string; required?: boolean; disabled?: boolean;
  onChange?: (value: { country: string; nationalNumber: string; value: string }) => void;
}) {
  const id = useId(); const initial = parsePhone(defaultValue);
  const [country, setCountry] = useState(initial.country); const [number, setNumber] = useState(initial.nationalNumber);
  const [touched, setTouched] = useState(false); const [inputError, setInputError] = useState('');
  const error = inputError || (touched ? phoneError(country, number, required) : '');
  function update(c: string, n: string) { setCountry(c); setNumber(n); onChange?.({ country: c, nationalNumber: n, value: normalizedPhone(c, n) }); }
  return <div className="phone-field"><label htmlFor={id}>{label}</label><div className="phone-control">
    <select name={`${name}Country`} aria-label={`País de ${label}`} value={country} disabled={disabled} onChange={e => { update(e.target.value, number); setTouched(true); }}>
      {countryPhoneRules.map(r => <option key={r.countryCode} value={r.countryCode}>{r.label} {r.dialCode}</option>)}
    </select><input id={id} name={name} type="tel" inputMode="numeric" pattern="[0-9]+" required={required} disabled={disabled}
      value={number} aria-invalid={!!error} aria-describedby={error ? `${id}-error` : undefined}
      onBlur={() => setTouched(true)} onChange={e => { if (/^[0-9]*$/.test(e.target.value)) { setInputError(''); update(country, e.target.value); } else setInputError('Introduce solo dígitos, sin código de país.'); }}
      onPaste={e => { e.preventDefault(); const text = e.clipboardData.getData('text'); if (!/^[0-9\s().-]+$/.test(text)) { setInputError('Pega solo el número nacional, sin letras ni código de país.'); return; } setInputError(''); update(country, text.replace(/[^0-9]/g, '')); setTouched(true); }} />
    </div>{error && <small id={`${id}-error`} role="alert">{error}</small>}</div>;
}
