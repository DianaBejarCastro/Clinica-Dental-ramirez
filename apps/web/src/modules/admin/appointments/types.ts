import { api } from '../../../services/api';

export const appointmentStates = {
  SCHEDULED: 'Programada', CONFIRMED: 'Confirmada', IN_PROGRESS: 'En atención',
  COMPLETED: 'Completada', CANCELLED: 'Cancelada', NO_SHOW: 'No asistió',
} as const;
export type AppointmentStatus = keyof typeof appointmentStates;
export interface Appointment {
  id: string; patientId: string; doctorId: string; centerId: string; specialtyId: string;
  appointmentDate: string; startTime: string; endTime: string; status: AppointmentStatus;
  reason: string | null; notes: string | null; createdByUserId: string | null; createdAt: string; updatedAt: string;
}
export interface Slot { startTime: string; endTime: string }
export interface Availability { doctorId: string; centerId: string; specialtyId: string; date: string; durationMinutes: number; slots: Slot[] }
export interface Option { id: string; name: string; isActive: boolean }
export async function allPages<T>(path: string): Promise<T[]> {
  const items: T[] = [];
  for (let page = 1; ; page++) {
    const result = await api<{ items: T[]; total: number }>(`${path}?page=${page}&pageSize=100`);
    items.push(...result.items);
    if (items.length >= result.total) return items;
    if (!result.items.length) throw new Error('El listado cambió durante la consulta. Vuelve a cargarlo.');
  }
}
export const slotKey = (slot: Slot) => `${slot.startTime}|${slot.endTime}`;
export const timeLabel = (time: string) => time.slice(0, 5);
