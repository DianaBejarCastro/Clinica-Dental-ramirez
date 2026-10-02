// E2E HTTP contra Docker real. Datos sintéticos conservados y desactivados al finalizar.
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { randomBytes, randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';

const env = Object.fromEntries(readFileSync(new URL('../.env', import.meta.url), 'utf8').split(/\r?\n/)
  .filter(line => /^[A-Z_]+=/.test(line)).map(line => { const at = line.indexOf('='); return [line.slice(0, at), line.slice(at + 1)]; }));
const base = `http://localhost:${env.API_PORT || 5000}`;
let access = ''; let cookie = ''; let checks = 0;
const stamp = randomUUID().replaceAll('-', '').slice(0, 12).toUpperCase();
const created = { centers: [], specialties: [], doctors: [], users: [] };
async function request(path, method = 'GET', body, expected = 200, token = access) {
  const response = await fetch(base + path, { method, headers: {
    'Content-Type': 'application/json', 'X-Clinic-Request': '1',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(path === '/api/auth/logout' && cookie ? { Cookie: cookie } : {}),
  }, body: body === undefined ? undefined : JSON.stringify(body) });
  assert.equal(response.status, expected, `${method} ${path}: HTTP ${response.status}, esperado ${expected}`);
  checks++;
  console.log(`PASS ${method} ${path} → ${expected}`);
  if (path === '/api/auth/login' && !access) cookie = response.headers.get('set-cookie')?.split(';')[0] ?? '';
  return response.status === 204 ? null : response.json().catch(() => null);
}
let originalClinic;
try {
  const login = await request('/api/auth/login', 'POST', { email: env.BOOTSTRAP_ADMIN_EMAIL, password: env.BOOTSTRAP_ADMIN_PASSWORD });
  access = login.accessToken;
  originalClinic = await request('/api/admin/clinic');
  await request('/api/admin/clinic', 'PUT', { ...originalClinic, description: `Verificación sintética v0.3.0 ${stamp}` });
  const centerBody = (name, code) => ({ name, code, address: 'Dirección sintética de verificación', country: 'Bolivia', city: 'La Paz', isMain: false, isActive: true });
  const central = await request('/api/admin/centers', 'POST', centerBody(`Central prueba ${stamp}`, `CENTRAL_${stamp}`), 201);
  created.centers.push(central.id);
  const north = await request('/api/admin/centers', 'POST', centerBody(`Norte prueba ${stamp}`, `NORTE_${stamp}`), 201);
  created.centers.push(north.id);
  await request(`/api/admin/centers/${central.id}`, 'PUT', { ...central, description: 'Sucursal sintética actualizada' });
  const specialty = await request('/api/admin/specialties', 'POST', { code: `TEST_${stamp}`, name: `Especialidad prueba ${stamp}`, description: 'Catálogo sintético', isActive: true }, 201);
  created.specialties.push(specialty.id);
  await request(`/api/admin/specialties/${specialty.id}`, 'PUT', { ...specialty, description: 'Catálogo sintético actualizado' });
  const catalog = await request('/api/admin/specialties');
  const general = catalog.find(s => s.code === 'GENERAL_DENTISTRY'); assert.ok(general?.isActive);
  const email = `doctor-${stamp.toLowerCase()}@example.com`;
  const password = randomBytes(32).toString('base64');
  const doctor = await request('/api/admin/doctors', 'POST', { newUser: { firstName: 'Doctor de prueba', lastName: stamp, email, password }, professionalLicense: `TEST-${stamp}`, bio: 'Perfil sintético de verificación', isActive: true }, 201);
  created.doctors.push(doctor.id); created.users.push(doctor.user.id);
  assert.ok(!JSON.stringify(doctor).includes(password)); assert.ok(!('passwordHash' in doctor.user));
  await request(`/api/admin/doctors/${doctor.id}/specialties`, 'PUT', { specialtyIds: [general.id, specialty.id, specialty.id] }, 204);
  assert.equal((await request(`/api/admin/doctors/${doctor.id}/specialties`)).length, 2);
  await request(`/api/admin/doctors/${doctor.id}/centers`, 'PUT', { centerIds: [central.id, north.id] }, 204);
  assert.equal((await request(`/api/admin/doctors/${doctor.id}/centers`)).length, 2);
  const block = (centerId, startTime, endTime) => ({ centerId, dayOfWeek: 1, startTime, endTime, hasBreak: false, breakStart: null, breakEnd: null, isActive: true });
  const morning = await request(`/api/admin/doctors/${doctor.id}/schedules`, 'POST', block(central.id, '08:00:00', '12:00:00'), 201);
  await request(`/api/admin/doctors/${doctor.id}/schedules/${morning.id}`, 'PUT', { ...block(central.id, '08:00:00', '12:00:00'), hasBreak: true, breakStart: '09:00:00', breakEnd: '09:30:00' });
  await request(`/api/admin/doctors/${doctor.id}/schedules/${morning.id}/status`, 'PATCH', { isActive: false }, 204);
  await request(`/api/admin/doctors/${doctor.id}/schedules/${morning.id}/status`, 'PATCH', { isActive: true }, 204);
  await request(`/api/admin/doctors/${doctor.id}/schedules`, 'POST', block(north.id, '14:00:00', '18:00:00'), 201);
  await request(`/api/admin/doctors/${doctor.id}/schedules`, 'POST', block(north.id, '11:00:00', '15:00:00'), 409);
  const updated = await request(`/api/admin/doctors/${doctor.id}`, 'PUT', { professionalLicense: doctor.professionalLicense, bio: 'Perfil sintético actualizado', isActive: true });
  assert.equal(updated.bio, 'Perfil sintético actualizado');
  const detail = await request(`/api/admin/doctors/${doctor.id}`);
  assert.equal(detail.specialties.length, 2); assert.equal(detail.centers.length, 2);
  assert.equal((await request(`/api/admin/doctors/${doctor.id}/schedules`)).length, 2);
  const dashboard = await request('/api/admin/dashboard');
  for (const key of ['totalUsers', 'totalDoctors', 'activeDoctors', 'totalCenters', 'activeCenters', 'totalSpecialties']) assert.equal(typeof dashboard[key], 'number');
  await request('/api/admin/clinic', 'GET', undefined, 401, '');
  const doctorSession = await request('/api/auth/login', 'POST', { email, password });
  assert.ok(doctorSession.user.roles.includes('DOCTOR'));
  await request('/api/admin/clinic', 'GET', undefined, 403, doctorSession.accessToken);
  await request(`/api/admin/doctors/${doctor.id}/schedules`, 'POST', block(central.id, '18:00:00', '19:00:00'), 403, doctorSession.accessToken);
  console.log('PASS relación real doctor ↔ dos especialidades ↔ dos sucursales; solapamiento rechazado.');
} finally {
  if (access) {
    for (const id of created.doctors) await request(`/api/admin/doctors/${id}/status`, 'PATCH', { isActive: false }, 204);
    for (const id of created.users) await request(`/api/admin/users/${id}/status`, 'PATCH', { isActive: false }, 204);
    for (const id of created.centers) await request(`/api/admin/centers/${id}/status`, 'PATCH', { isActive: false }, 204);
    for (const id of created.specialties) await request(`/api/admin/specialties/${id}/status`, 'PATCH', { isActive: false }, 204);
    if (originalClinic) await request('/api/admin/clinic', 'PUT', originalClinic);
    await request('/api/auth/logout', 'POST', undefined, 204);
  }
}
mkdirSync('artifacts', { recursive: true });
writeFileSync('artifacts/clinic-e2e.json', JSON.stringify({ completedAt: new Date().toISOString(), checks, created }, null, 2));
console.log(`E2E v0.3.0: ${checks} comprobaciones HTTP; datos sintéticos conservados e inactivos.`);
