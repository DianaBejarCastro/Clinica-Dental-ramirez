export const COVER_WIDTH = 1916;
export const COVER_HEIGHT = 821;
export const COVER_MAX_BYTES = 10 * 1024 * 1024;
export const clamp = (value: number, min = 0, max = 1) => Math.max(min, Math.min(max, value));

// Coordinates are in source pixels. The minimum zoom always fills the frame.
export function coverCrop(width: number, height: number, zoom: number, x: number, y: number) {
  const scale = Math.max(COVER_WIDTH / width, COVER_HEIGHT / height) * clamp(zoom, 1, 3);
  const w = Math.min(width, COVER_WIDTH / scale), h = Math.min(height, COVER_HEIGHT / scale);
  return { x: (width - w) * clamp(x), y: (height - h) * clamp(y), width: w, height: h };
}
export async function renderCover(image: HTMLImageElement, zoom: number, x: number, y: number, type: string): Promise<Blob> {
  const crop = coverCrop(image.naturalWidth, image.naturalHeight, zoom, x, y);
  const canvas = document.createElement('canvas'); canvas.width = COVER_WIDTH; canvas.height = COVER_HEIGHT;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('No se pudo preparar la imagen.');
  context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high';
  context.drawImage(image, crop.x, crop.y, crop.width, crop.height, 0, 0, COVER_WIDTH, COVER_HEIGHT);
  return new Promise((resolve, reject) => canvas.toBlob(blob => {
    if (!blob || blob.size > COVER_MAX_BYTES) reject(new Error('La imagen final debe ocupar como máximo 10 MB.'));
    else resolve(blob);
  }, type, 0.94));
}
