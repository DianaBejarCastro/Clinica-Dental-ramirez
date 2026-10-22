import type { FieldSpec } from '../shared';

export const personalFields: FieldSpec[] = [
  { name: 'firstName', label: 'Nombres', required: true, maxLength: 100 },
  { name: 'lastName', label: 'Apellidos', required: true, maxLength: 100 },
  { name: 'documentNumber', label: 'Número de documento', required: true, maxLength: 50 },
  { name: 'birthDate', label: 'Fecha de nacimiento', type: 'date', required: true },
  { name: 'gender', label: 'Género', maxLength: 50 },
  { name: 'phone', label: 'Teléfono', required: true, maxLength: 30 },
  { name: 'address', label: 'Dirección', maxLength: 500 },
];

export const accountFields: FieldSpec[] = [{ name: 'email', label: 'Email de acceso', type: 'email', required: true, maxLength: 254 }];
