export type MediaType = 'IMAGE' | 'VIDEO_UPLOAD' | 'TIKTOK';
export interface MediaItem { id: string; mediaType: MediaType; title: string | null; caption: string | null; altText: string | null; externalUrl: string | null; width: number | null; height: number | null; sortOrder?: number; isActive?: boolean }
export interface MediaPage { items: MediaItem[]; total: number; page: number; pageSize: number; videoUploadAvailable: boolean }
export const mediaFilters = [['', 'Todas'], ['IMAGE', 'Fotos'], ['VIDEO_UPLOAD', 'Videos'], ['TIKTOK', 'TikTok']] as const;
