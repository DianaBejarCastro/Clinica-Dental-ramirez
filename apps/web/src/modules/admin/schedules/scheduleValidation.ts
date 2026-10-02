import type { DoctorSchedule } from '../types';
export const days = ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo'];
export const timeLabel = (value: string | null) => value?.slice(0, 5) ?? '—';
export interface ScheduleInput { centerId: string; dayOfWeek: number; startTime: string; endTime: string; hasBreak: boolean; breakStart: string | null; breakEnd: string | null; isActive: boolean }
function timeSeconds(value: string | null) {
  if (!value || !/^([01]\d|2[0-3]):[0-5]\d(?::[0-5]\d(?:\.\d+)?)?$/.test(value)) return null;
  const [hours, minutes, seconds = '0'] = value.split(':');
  return Number(hours) * 3600 + Number(minutes) * 60 + Number(seconds);
}
export function validateSchedule(input: ScheduleInput, schedules: DoctorSchedule[], excludeId?: string) {
  if (!input.centerId) return 'Selecciona una sucursal asignada al doctor.';
  if (!Number.isInteger(input.dayOfWeek) || input.dayOfWeek < 1 || input.dayOfWeek > 7) return 'Selecciona un día entre lunes y domingo.';
  const start = timeSeconds(input.startTime); const end = timeSeconds(input.endTime);
  if (start === null || end === null || start >= end) return 'La hora de inicio debe ser anterior a la hora de fin.';
  const breakStart = timeSeconds(input.breakStart); const breakEnd = timeSeconds(input.breakEnd);
  if (input.hasBreak && (breakStart === null || breakEnd === null || !(start < breakStart && breakStart < breakEnd && breakEnd < end))) return 'El descanso debe empezar y terminar dentro del bloque, con inicio anterior al fin.';
  if (input.isActive && schedules.some(block => block.id !== excludeId && block.isActive && block.dayOfWeek === input.dayOfWeek && start < (timeSeconds(block.endTime) ?? 0) && (timeSeconds(block.startTime) ?? 0) < end)) return 'Este horario se solapa con otro bloque del doctor, incluso si pertenece a otra sucursal.';
  return '';
}
