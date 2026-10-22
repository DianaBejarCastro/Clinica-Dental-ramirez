import { fireEvent, render, screen, within } from '@testing-library/react';
import { expect, it } from 'vitest';
import IdentityDocumentFields, { documentData, documentLabel } from '../components/IdentityDocumentFields';
import { PatientFields } from '../modules/patient/PatientFields';
import StaffIdentityFields from '../modules/admin/users/StaffIdentityFields';

it('patient identity fields have the required order and text number', () => {
  const { container } = render(<form><PatientFields /></form>);
  expect([...container.querySelectorAll('input:not([type=hidden]),select')].slice(0, 6).map(e => e.getAttribute('name')))
    .toEqual(['firstName', 'lastName', 'documentType', 'documentNumber', 'birthDate', 'gender']);
  const types = screen.getByLabelText('Tipo de documento');
  expect(within(types).getAllByRole('option').map(e => (e as HTMLOptionElement).value)).toEqual(['CI', 'PASSPORT']);
  expect(screen.getByLabelText('Número de documento').getAttribute('type')).toBe('text');
});
it('CI submits BO without a country selector', () => {
  const { container } = render(<form><IdentityDocumentFields /></form>);
  fireEvent.change(screen.getByLabelText('Número de documento'), { target: { value: '001234' } });
  expect(screen.queryByLabelText('País emisor')).toBeNull();
  expect(documentData(new FormData(container.querySelector('form')!))).toEqual({ documentType: 'CI', documentNumber: '001234', documentCountryCode: 'BO' });
});
it('passport accepts alphanumeric text and requires issuing country', () => {
  const { container } = render(<form><IdentityDocumentFields /></form>);
  fireEvent.change(screen.getByLabelText('Tipo de documento'), { target: { value: 'PASSPORT' } });
  const country = screen.getByLabelText('País emisor') as HTMLSelectElement;
  expect(country.required).toBe(true); expect(country.value).toBe('');
  fireEvent.change(country, { target: { value: 'US' } });
  fireEvent.change(screen.getByLabelText('Número de documento'), { target: { value: ' pa009ab ' } });
  expect(documentData(new FormData(container.querySelector('form')!))).toEqual({ documentType: 'PASSPORT', documentNumber: 'PA009AB', documentCountryCode: 'US' });
});
it('editing preserves passport identity and switches to CI country', () => {
  const { container } = render(<form><IdentityDocumentFields value={{ documentType: 'PASSPORT', documentNumber: 'X009283', documentCountryCode: 'PE' }} /></form>);
  expect((screen.getByLabelText('País emisor') as HTMLSelectElement).value).toBe('PE');
  expect((screen.getByLabelText('Número de documento') as HTMLInputElement).value).toBe('X009283');
  fireEvent.change(screen.getByLabelText('Tipo de documento'), { target: { value: 'CI' } });
  expect(documentData(new FormData(container.querySelector('form')!)).documentCountryCode).toBe('BO');
});
it('staff uses the same identity component', () => {
  render(<StaffIdentityFields value={{ documentType: 'PASSPORT', documentNumber: 'A00123', documentCountryCode: 'AR' }} />);
  expect(screen.getAllByLabelText('Tipo de documento')).toHaveLength(1);
  expect((screen.getByLabelText('País emisor') as HTMLSelectElement).value).toBe('AR');
});
it('document display distinguishes passport from CI without exposing IDs', () => {
  expect(documentLabel({ documentType: 'PASSPORT', documentNumber: 'PA123' })).toBe('Pasaporte · PA123');
  expect(documentLabel({ documentType: 'CI', documentNumber: '123' })).toBe('CI · 123');
  expect(documentLabel({})).toBe('—');
});
