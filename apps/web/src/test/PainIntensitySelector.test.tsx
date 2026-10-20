import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PainIntensitySelector from '../modules/doctor/clinical/PainIntensitySelector';
afterEach(()=>{cleanup();vi.unstubAllGlobals();});
it('offers only eleven labeled radios and no free text',()=>{
  render(<PainIntensitySelector/>);
  expect(screen.getByRole('group',{name:'Intensidad del dolor (0–10)'})).toBeTruthy();
  expect(screen.getAllByRole('radio')).toHaveLength(11);
  for(let n=0;n<=10;n++)expect((screen.getByRole('radio',{name:String(n)}) as HTMLInputElement).value).toBe(String(n));
  expect(screen.queryByRole('spinbutton')).toBeNull();expect(screen.queryByRole('textbox')).toBeNull();
});
it('preselects existing value and replaces it on selection',async()=>{
  const {container}=render(<form><PainIntensitySelector defaultValue={5}/></form>);
  expect((screen.getByRole('radio',{name:'5'}) as HTMLInputElement).checked).toBe(true);
  await userEvent.click(screen.getByRole('radio',{name:'8'}));
  expect(container.querySelectorAll('input:checked')).toHaveLength(1);
  expect(new FormData(container.querySelector('form')!).get('painIntensity')).toBe('8');
});
it('supports keyboard arrow selection with one checked value',async()=>{
  // JSDOM lacks CSS.escape; user-event needs it to find this alphanumeric radio group.
  vi.stubGlobal('CSS',{escape:(name:string)=>{expect(name).toBe('painIntensity');return name;}});
  const {container}=render(<PainIntensitySelector defaultValue={3}/>);
  screen.getByRole('radio',{name:'3'}).focus();await userEvent.keyboard('{ArrowRight}');
  expect((screen.getByRole('radio',{name:'4'}) as HTMLInputElement).checked).toBe(true);
  expect(container.querySelectorAll('input:checked')).toHaveLength(1);
});
it('keeps omitted intensity distinct from zero and supports required usage',()=>{
  const {container,rerender}=render(<form><PainIntensitySelector/></form>);
  expect(new FormData(container.querySelector('form')!).get('painIntensity')).toBeNull();
  expect(container.querySelector('form')!.checkValidity()).toBe(true);
  rerender(<form><PainIntensitySelector required/></form>);
  expect(container.querySelector('form')!.checkValidity()).toBe(false);
});
