export interface NavigationItem { label: string; route: string; permission?: string }
export interface NavigationGroup { label: string; items: NavigationItem[] }
export interface NavigationConfiguration { items: NavigationItem[]; groups: NavigationGroup[] }
export const doctorNavigation: NavigationConfiguration = { items: [{ label: 'Mis citas', route: '/doctor/appointments' }], groups: [] };
export const overview: NavigationItem = { label: 'Resumen', route: '/' };
export const navigationGroups: NavigationGroup[] = [
  { label: 'Administración', items: [
    { label: 'Administradores', route: '/administrators', permission: 'users.view' },
    { label: 'Cajeros', route: '/cashiers', permission: 'users.view' },
    { label: 'Secretarias', route: '/secretaries', permission: 'users.view' },
    { label: 'Usuarios', route: '/users', permission: 'users.view' },
    { label: 'Roles', route: '/roles', permission: 'roles.view' },
    { label: 'Permisos', route: '/permissions', permission: 'permissions.view' },
  ] },
  { label: 'Clínica', items: [
    { label: 'Datos de la clínica', route: '/clinic', permission: 'clinic.view' },
    { label: 'Sucursales', route: '/centers', permission: 'centers.view' },
    { label: 'Especialidades', route: '/specialties', permission: 'specialties.view' },
  ] },
  { label: 'Equipo médico', items: [
    { label: 'Doctores', route: '/doctors', permission: 'doctors.view' },
    { label: 'Horarios', route: '/schedules', permission: 'schedules.view' },
  ] },
  { label: 'Atención', items: [
    { label: 'Pacientes', route: '/patients', permission: 'patients.view' },
    { label: 'Agenda / Citas', route: '/appointments', permission: 'appointments.view' },
  ] },
];

export const cashierNavigation = { items: [{ label: "Cobros", route: "/cashier/payments", permission: "payments.view" }], groups: [] };
