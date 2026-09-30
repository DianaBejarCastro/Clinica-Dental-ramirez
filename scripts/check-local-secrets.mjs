import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
const git = (...args) => execFileSync('git', ['-c', `safe.directory=${process.cwd().replaceAll('\\', '/')}`, ...args], { encoding: 'utf8' });
const secrets = readFileSync('.env', 'utf8').split(/\r?\n/).filter(line => /^(POSTGRES_PASSWORD|JWT_SECRET|BOOTSTRAP_ADMIN_PASSWORD)=/.test(line)).map(line => line.slice(line.indexOf('=') + 1)).filter(value => value && value !== 'CHANGE_ME');
const files = [...new Set(git('ls-files', '--cached', '--others', '--exclude-standard', '-z').split('\0').filter(Boolean))];
const matches = files.filter(file => { const contents = readFileSync(file, 'utf8'); return secrets.some(secret => contents.includes(secret)); });
if (matches.length) { console.error('Valores secretos locales encontrados en:', matches.join(', ')); process.exitCode = 1; }
else console.log(`PASS: ${files.length} archivos candidatos revisados; ningún valor secreto de .env encontrado.`);
