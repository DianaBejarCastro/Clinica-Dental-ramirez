export function validCoordinates(latitude: number | null, longitude: number | null) {
  return latitude !== null && longitude !== null && Number.isFinite(latitude) && Number.isFinite(longitude)
    && latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
}
export function googleMapsLink(latitude: number | null, longitude: number | null) {
  return validCoordinates(latitude, longitude)
    ? `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}` : null;
}
