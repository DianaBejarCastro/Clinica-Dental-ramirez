// Smoke real de React + API + cookies usando Edge instalado y CDP, sin dependencias adicionales.
import { spawn } from 'node:child_process';
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { randomInt, randomUUID } from 'node:crypto';
import assert from 'node:assert/strict';

const env = Object.fromEntries(readFileSync(new URL('../.env', import.meta.url), 'utf8').split(/\r?\n/).filter(line => /^[A-Z_]+=/.test(line)).map(line => { const at = line.indexOf('='); return [line.slice(0, at), line.slice(at + 1)]; }));
const port = randomInt(20000, 30000);
const profile = resolve('artifacts', `edge-smoke-${randomUUID()}`);
mkdirSync(profile, { recursive: true });
const browser = spawn(process.env.EDGE_PATH ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe', ['--headless', '--disable-gpu', '--no-first-run', `--user-data-dir=${profile}`, `--remote-debugging-port=${port}`, '--window-size=1440,1000', `http://localhost:${env.WEB_PORT || 5173}/`], { windowsHide: true, stdio: 'ignore' });
const pause = () => new Promise(r => setTimeout(r, 200));
let socket;
try {
  let target;
  for (let n = 0; n < 100; n++) {
    try { target = (await (await fetch(`http://localhost:${port}/json`)).json()).find(t => t.type === 'page'); } catch { /* browser starting */ }
    if (target) break;
    await pause();
  }
  assert.ok(target, 'Edge debe exponer una página CDP');
  socket = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((res, rej) => { socket.addEventListener('open', res, { once: true }); socket.addEventListener('error', rej, { once: true }); });
  let sequence = 0; const pending = new Map();
  socket.addEventListener('message', event => { const msg = JSON.parse(event.data); const entry = pending.get(msg.id); if (entry) { pending.delete(msg.id); clearTimeout(entry.timer); msg.error ? entry.reject(new Error('CDP request failed')) : entry.resolve(msg.result); } });
  function command(method, params = {}) { return new Promise((resolve, reject) => { const id = ++sequence; const timer = setTimeout(() => { pending.delete(id); reject(new Error(`Timeout CDP ${method}`)); }, 15000); pending.set(id, { resolve, reject, timer }); socket.send(JSON.stringify({ id, method, params })); }); }
  async function evaluate(expression) { const result = await command('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true }); assert.ok(!result.exceptionDetails, 'JavaScript del smoke debe ejecutarse sin excepciones'); return result.result.value; }
  async function until(expression, description) { for (let n = 0; n < 75; n++) { if (await evaluate(expression)) return; await pause(); } throw new Error(`No se verificó: ${description}`); }
  await until('!!document.querySelector("input[name=email]")', 'formulario login');
  console.log('PASS navegador: login renderizado');
  await evaluate(`(() => { const set = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set; for (const [name,value] of ${JSON.stringify([['email', env.BOOTSTRAP_ADMIN_EMAIL], ['password', env.BOOTSTRAP_ADMIN_PASSWORD]])}) { const input = document.querySelector('input[name='+name+']'); set.call(input,value); input.dispatchEvent(new Event('input',{bubbles:true})); } document.querySelector('form button').click(); })()`);
  await until('!!document.querySelector("nav")', 'login real');
  assert.equal(await evaluate('localStorage.length + sessionStorage.length'), 0);
  const cookies = await command('Network.getAllCookies');
  const cookie = cookies.cookies.find(c => c.name === 'clinic_refresh');
  assert.ok(cookie?.httpOnly); assert.equal(cookie.sameSite, 'Strict');
  console.log('PASS navegador: login real, cookie HttpOnly/Strict y tokens fuera del storage');
  for (const [route, text] of [['/clinic', 'Razón social'], ['/centers', 'Sucursales'], ['/doctors', 'Doctores'], ['/specialties', 'Odontología general'], ['/schedules', 'Lunes']]) {
    await evaluate(`location.hash=${JSON.stringify(route)}`);
    await until(`document.body.innerText.includes(${JSON.stringify(text)})`, route);
    console.log(`PASS navegador: ${route}`);
  }
  await command('Page.reload');
  await until('!!document.querySelector("nav") && document.body.innerText.includes("Lunes")', 'restauración de sesión');
  console.log('PASS navegador: sesión restaurada después de recargar');
  await evaluate('location.hash="/users"');
  await until('document.body.innerText.includes("usuarios · Página")', 'usuarios');
  const screenshot = await command('Page.captureScreenshot', { format: 'png' });
  writeFileSync(resolve('artifacts/admin-v030.png'), Buffer.from(screenshot.data, 'base64'));
  await evaluate('[...document.querySelectorAll("button")].find(b => b.textContent === "Cerrar sesión").click()');
  await until('!!document.querySelector("input[name=password]")', 'logout');
  console.log('PASS navegador: logout y ruta protegida');
  await command('Browser.close');
} finally { socket?.close(); browser.kill(); }
