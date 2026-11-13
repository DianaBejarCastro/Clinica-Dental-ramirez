import { expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import PhoneInput from '../components/PhoneInput';
import { parsePhone, phoneError, normalizedPhone } from '../components/phoneRules';
it('defaults to Bolivia and exposes accessible numeric input', () => {
 render(<PhoneInput name="phone" label="Teléfono" required />);
 const input=screen.getByLabelText('Teléfono') as HTMLInputElement;
 expect(input.inputMode).toBe('numeric');expect(input.pattern).toBe('[0-9]+');
 expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe('BO');expect(screen.getByText('Bolivia +591')).toBeTruthy();
 fireEvent.change(input,{target:{value:'65700344'}});fireEvent.change(input,{target:{value:'65A00344'}});
 expect(input.value).toBe('65700344');expect(input.getAttribute('aria-describedby')).toBeTruthy();
});
it.each(['6570034','657003444'])('rejects invalid Bolivia length %s', number => {expect(phoneError('BO',number)).toContain('8 dígitos');});
it('accepts eight digits and normalizes both legacy and international values',()=>{
 expect(phoneError('BO','65700344')).toBe('');expect(normalizedPhone('BO','65700344')).toBe('+59165700344');
 expect(parsePhone('65700344')).toEqual(parsePhone('+59165700344'));
});
it('cleans decorative paste but rejects letters and international prefixes',()=>{
 render(<PhoneInput name="emergencyContactPhone" label="Emergency Contact Phone Number" required />);
 const input=screen.getByLabelText('Emergency Contact Phone Number') as HTMLInputElement;
 for(const value of ['6570 0344','(6570)-0344']) {fireEvent.paste(input,{clipboardData:{getData:()=>value}});expect(input.value).toBe('65700344');}
 for(const value of ['65A00344','+59165700344']) {fireEvent.paste(input,{clipboardData:{getData:()=>value}});expect(input.value).toBe('65700344');expect(screen.getByRole('alert')).toBeTruthy();}
});
it('country selection changes normalized dial code',()=>{
 const onChange=vi.fn();render(<PhoneInput name="phone" label="Teléfono" onChange={onChange} />);
 fireEvent.change(screen.getByRole('combobox'),{target:{value:'PE'}});fireEvent.change(screen.getByLabelText('Teléfono'),{target:{value:'987654321'}});
 expect(onChange).toHaveBeenLastCalledWith({country:'PE',nationalNumber:'987654321',value:'+51987654321'});
});
