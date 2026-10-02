export interface Patient {
  id: string;
  userId: string | null;
  firstName: string;
  lastName: string;
  ci: string | null;
  birthDate: string | null;
  gender: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  bloodType: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}
export interface PatientList { items: Patient[]; total: number; page: number; pageSize: number }
