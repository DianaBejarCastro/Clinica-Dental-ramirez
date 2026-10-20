import type { ClinicLogoMetadata } from './clinic/logoResource';
export interface Timestamped { id: string; isActive: boolean; createdAt: string; updatedAt: string | null }
export interface Clinic extends Timestamped { logo?: ClinicLogoMetadata | null; name: string; legalName: string | null; description: string | null; logoUrl: string | null; phone: string | null; whatsapp: string | null; email: string | null; taxId: string | null }
export interface Center extends Timestamped { clinicId: string; name: string; code: string; description: string | null; address: string; city: string | null; department: string | null; country: string; phone: string | null; whatsapp: string | null; email: string | null; latitude: number | null; longitude: number | null; isMain: boolean }
export interface Specialty extends Timestamped { code: string; name: string; description: string | null; doctorCount: number }
export interface Assignment { id: string; code: string; name: string; isActive: boolean }
export interface Doctor extends Timestamped { userId: string; user: { id: string; firstName: string; lastName: string; email: string; isActive: boolean }; professionalLicense: string | null; ci: string | null; birthDate: string | null; gender: string | null; phone: string | null; address: string | null; photoUrl: string | null; bio: string | null; specialties: Assignment[]; centers: Assignment[] }
export interface DoctorSchedule extends Timestamped { doctorId: string; centerId: string; centerName: string; dayOfWeek: number; startTime: string; endTime: string; hasBreak: boolean; breakStart: string | null; breakEnd: string | null }
export interface DoctorPage { items: Doctor[]; total: number; page: number; pageSize: number }
export interface Dashboard { totalUsers: number | null; totalDoctors: number | null; activeDoctors: number | null; totalCenters: number | null; activeCenters: number | null; totalSpecialties: number | null }
