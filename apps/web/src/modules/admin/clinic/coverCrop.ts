export const COVER_WIDTH = 1916;
export const COVER_HEIGHT = 821;
export const COVER_MAX_BYTES = 10 * 1024 * 1024;
export const clamp = (value: number, min = 0, max = 1) => Math.max(min, Math.min(max, value));

// Coordinates are in source pixels. The minimum zoom always fills the frame.
export function coverCrop(width: number, height: number, zoom: number, x: number, y: number, targetWidth = COVER_WIDTH, targetHeight = COVER_HEIGHT) {
  const scale = Math.max(targetWidth / width, targetHeight / height) * clamp(zoom, 1, 3);
  const w = Math.min(width, targetWidth / scale), h = Math.min(height, targetHeight / scale);
  return { x: (width - w) * clamp(x), y: (height - h) * clamp(y), width: w, height: h };
}
export async function renderCover(image: HTMLImageElement, zoom: number, x: number, y: number, type: string, targetWidth = COVER_WIDTH, targetHeight = COVER_HEIGHT): Promise<Blob> {
  const crop = coverCrop(image.naturalWidth, image.naturalHeight, zoom, x, y, targetWidth, targetHeight);
  const canvas = document.createElement('canvas'); canvas.width = targetWidth; canvas.height = targetHeight;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('No se pudo preparar la imagen.');
  context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high';
  context.drawImage(image, crop.x, crop.y, crop.width, crop.height, 0, 0, targetWidth, targetHeight);
  return new Promise((resolve, reject) => canvas.toBlob(blob => {
    if (!blob || blob.size > COVER_MAX_BYTES) reject(new Error('La imagen final debe ocupar como máximo 10 MB.'));
    else resolve(blob);
  }, type, 0.94));
}
