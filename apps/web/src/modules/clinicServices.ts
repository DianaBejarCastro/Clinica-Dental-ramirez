export const serviceVariants = { TEAL: 'Teal', CYAN: 'Cian', NAVY: 'Azul marino', AQUA: 'Aqua', MINT: 'Menta', DARK_TEAL: 'Teal oscuro' } as const;
export type ServiceVariant = keyof typeof serviceVariants;
export interface PublicClinicService { id: string; name: string; shortDescription: string; visualVariant: ServiceVariant }
export interface ClinicService extends PublicClinicService { displayOrder: number; isActive: boolean; isPublic: boolean; specialtyId: string | null }
