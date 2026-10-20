"""Explicit local-development reset. Preview by default; --execute authorizes deletion.

All non-primary users are assumed synthetic ONLY for this development dataset.
Unknown tables/FKs/triggers abort. Audit actors are detached explicitly, retaining
their original UUID in metadata. No schema changes or cascading deletes.
Requires Python, Docker Compose and the existing Development API container.
"""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parent.parent
os.chdir(ROOT)
ADMIN = '676fdafe-e1c9-410a-958e-866ebde02724'
EMAIL = 'admin@example.com'
OPERATIONAL = ['Appointments', 'DoctorSchedules', 'DoctorCenters', 'DoctorSpecialties',
               'PatientProfiles', 'DoctorProfiles', 'DentalAssistantProfiles']
PRESERVED = ['Specialties', 'Centers', 'Clinics', 'ClinicSocialNetworks', 'Roles',
             'Permissions', 'RolePermissions', '__EFMigrationsHistory']
TABLES = OPERATIONAL + PRESERVED + ['Users', 'UserRoles', 'RefreshTokens', 'AuditLogs']

def run(args, data=None):
    result = subprocess.run(args, input=data, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        # SQL errors may contain row details. Never print stderr or SQL contents.
        raise RuntimeError('Command failed (details suppressed to protect personal data): ' + args[0])
    return result.stdout

def sql(query):
    return run(['docker', 'compose', 'exec', '-T', 'postgres', 'psql', '-U',
                'clinica_user', '-d', 'clinica_ramirez', '-X', '-qAt', '-v',
                'ON_ERROR_STOP=1'], query.encode()).decode().strip()

def guard_environment():
    for key in ['ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'ENVIRONMENT']:
        if os.environ.get(key, 'Development').lower() != 'development':
            raise RuntimeError('Refusing non-Development process environment.')
    env = dict(line.split('=', 1) for line in (ROOT / '.env').read_text(encoding='utf-8-sig').splitlines()
               if '=' in line and not line.startswith('#'))
    if env.get('POSTGRES_DB') != 'clinica_ramirez' or env.get('BOOTSTRAP_ADMIN_EMAIL') != EMAIL:
        raise RuntimeError('Unexpected database or bootstrap administrator.')
    for key in ['ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT', 'ENVIRONMENT']:
        if env.get(key, 'Development').lower() != 'development':
            raise RuntimeError('Refusing non-Development dotenv environment.')
    if env.get('DOCKER_HOST') or os.environ.get('DOCKER_HOST'):
        raise RuntimeError('Custom Docker host is not supported.')
    context = json.loads(run(['docker', 'context', 'inspect']))[0]
    if not context['Endpoints']['docker']['Host'].startswith('npipe://'):
        raise RuntimeError('Only the local Windows Docker engine is supported.')
    cid = run(['docker', 'compose', 'ps', '-q', 'api']).decode().strip()
    api = json.loads(run(['docker', 'inspect', cid]))[0]
    settings = dict(x.split('=', 1) for x in api['Config']['Env'] if '=' in x)
    if settings.get('ASPNETCORE_ENVIRONMENT') != 'Development':
        raise RuntimeError('API is not Development.')
    cid = run(['docker', 'compose', 'ps', '-q', 'postgres']).decode().strip()
    pg = json.loads(run(['docker', 'inspect', cid]))[0]
    ports = pg['NetworkSettings']['Ports'].get('5432/tcp') or []
    if not ports or any(x['HostIp'] != '127.0.0.1' for x in ports):
        raise RuntimeError('PostgreSQL must be published only on loopback.')

def main():
    guard_environment()
    actual = json.loads(sql("SELECT json_agg(tablename ORDER BY tablename) FROM pg_tables WHERE schemaname='public';"))
    if sorted(actual) != sorted(TABLES):
        raise RuntimeError('Unexpected tables; inspect dependencies before extending this reset.')
    fks = json.loads(sql("SELECT json_agg(pg_get_constraintdef(oid) ORDER BY conname) FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace;"))
    if len(fks) != 25 or any('ON DELETE RESTRICT' not in x for x in fks):
        raise RuntimeError('Unexpected FK model; manual review required.')
    links = json.loads(sql("SELECT json_agg(conrelid::regclass::text || ':' || pg_get_constraintdef(oid) ORDER BY conname) FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace;"))
    expected = {
        'Appointments': [('CenterId','Centers','Id'),('CreatedByUserId','Users','Id'),('SpecialtyId','Specialties','Id'),('PatientId','PatientProfiles','Id'),('DoctorId','DoctorProfiles','Id')],
        'AuditLogs': [('UserId','Users','Id')], 'Centers': [('ClinicId','Clinics','Id')],
        'ClinicSocialNetworks': [('ClinicId','Clinics','Id')], 'DentalAssistantProfiles': [('UserId','Users','Id')],
        'DoctorCenters': [('CenterId','Centers','Id'),('DoctorId','DoctorProfiles','Id')],
        'DoctorProfiles': [('UserId','Users','Id')],
        'DoctorSchedules': [('DoctorId,CenterId','DoctorCenters','DoctorId,CenterId'),('DoctorId','DoctorProfiles','Id'),('CenterId','Centers','Id')],
        'DoctorSpecialties': [('SpecialtyId','Specialties','Id'),('DoctorId','DoctorProfiles','Id')],
        'PatientProfiles': [('UserId','Users','Id')],
        'RefreshTokens': [('UserId','Users','Id'),('ReplacedByTokenId','RefreshTokens','Id')],
        'RolePermissions': [('RoleId','Roles','Id'),('PermissionId','Permissions','Id')],
        'UserRoles': [('RoleId','Roles','Id'),('UserId','Users','Id'),('AssignedByUserId','Users','Id')],
    }
    def columns(value):
        return ', '.join('"' + column + '"' for column in value.split(','))
    expected_links = [f'"{table}":FOREIGN KEY ({columns(source)}) REFERENCES "{target}"({columns(destination)}) ON DELETE RESTRICT'
                      for table, definitions in expected.items() for source,target,destination in definitions]
    if sorted(links) != sorted(expected_links):
        raise RuntimeError('FK endpoints changed; inspect before permitting a reset.')
    # Preserve the exact FK definitions and whole schema, not just their count.
    schema = run(['docker', 'compose', 'exec', '-T', 'postgres', 'pg_dump', '-U', 'clinica_user', '-d', 'clinica_ramirez', '--schema-only'])
    counts = {t: int(sql(f'SELECT count(*) FROM "{t}";')) for t in TABLES}
    targets = {t: counts[t] for t in OPERATIONAL}
    for t in ['Users', 'UserRoles', 'RefreshTokens']:
        col = 'Id' if t == 'Users' else 'UserId'
        targets[t] = int(sql(f'SELECT count(*) FROM "{t}" WHERE "{col}" <> \'{ADMIN}\';'))
    print(json.dumps({'environment': 'Development/local', 'protectedAdmin': {'id': ADMIN, 'email': EMAIL},
                      'before': counts, 'delete': targets, 'fkCount': len(fks)}, indent=2), flush=True)
    admin_filter = f'"Id" = \'{ADMIN}\''
    checks = f"""
      IF NOT EXISTS (SELECT 1 FROM "Users" WHERE {admin_filter} AND "Email"='{EMAIL}' AND "IsActive" AND "CanLogin") THEN RAISE EXCEPTION 'Protected admin invalid'; END IF;
      IF (SELECT count(*) FROM "UserRoles" ur JOIN "Roles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"='{ADMIN}' AND r."Code"='ADMINISTRATOR' AND r."IsActive") <> 1 THEN RAISE EXCEPTION 'Admin role invalid'; END IF;
      IF EXISTS (SELECT 1 FROM "UserRoles" ur JOIN "Roles" r ON r."Id"=ur."RoleId" WHERE ur."UserId"='{ADMIN}' AND r."Code" <> 'ADMINISTRATOR') THEN RAISE EXCEPTION 'Primary admin has other roles'; END IF;
      IF EXISTS (SELECT 1 FROM "UserRoles" WHERE "UserId"='{ADMIN}' AND "AssignedByUserId" <> '{ADMIN}') THEN RAISE EXCEPTION 'Protected role depends on deleted actor'; END IF;
      IF EXISTS (SELECT 1 FROM "RefreshTokens" a JOIN "RefreshTokens" b ON a."ReplacedByTokenId"=b."Id" WHERE a."UserId"='{ADMIN}' AND b."UserId" <> '{ADMIN}') THEN RAISE EXCEPTION 'Protected session depends on deleted session'; END IF;
      IF EXISTS (SELECT 1 FROM pg_trigger WHERE NOT tgisinternal AND tgrelid IN (SELECT oid FROM pg_class WHERE relnamespace='public'::regnamespace)) THEN RAISE EXCEPTION 'Unexpected trigger'; END IF;
      IF EXISTS (SELECT 1 FROM "AuditLogs" WHERE "UserId" <> '{ADMIN}' AND "MetadataJson" IS NOT NULL AND (jsonb_typeof("MetadataJson") <> 'object' OR "MetadataJson" ? 'developmentResetActorId')) THEN RAISE EXCEPTION 'Audit metadata requires review'; END IF;
    """
    for t in ['DoctorProfiles', 'PatientProfiles', 'DentalAssistantProfiles']:
        checks += f"IF EXISTS (SELECT 1 FROM \"{t}\" WHERE \"UserId\"='{ADMIN}') THEN RAISE EXCEPTION 'Protected admin has profile'; END IF;\n"
    sql('DO $$ BEGIN ' + checks + ' END $$;')
    if '--execute' not in sys.argv:
        print('Preview only. --execute runs the authorized synthetic-data reset.')
        return
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    folder = ROOT / 'database' / 'backups'
    folder.mkdir(parents=True, exist_ok=True)
    backup = folder / f'before-test-reset-{stamp}.dump'
    backup.write_bytes(run(['docker', 'compose', 'exec', '-T', 'postgres', 'pg_dump', '-U', 'clinica_user', '-d', 'clinica_ramirez', '-Fc']))
    if not backup.read_bytes().startswith(b'PGDMP'):
        raise RuntimeError('Backup is invalid.')
    (folder / f'before-test-reset-{stamp}.schema.sql').write_bytes(schema)
    print('Backup created:', backup.name, flush=True)
    reset_id = str(uuid.uuid4())
    statements = ['BEGIN;', "SET LOCAL lock_timeout='15s';", "SET LOCAL statement_timeout='60s';",
                  'SELECT pg_advisory_xact_lock(820020);',
                  'LOCK TABLE ' + ','.join(f'"{t}"' for t in TABLES) + ' IN SHARE ROW EXCLUSIVE MODE;',
                  'DO $$ BEGIN ' + checks + ' END $$;']
    # All snapshots are TEMP tables dropped at commit, with no permanent schema change.
    for t in PRESERVED:
        statements.append(f'CREATE TEMP TABLE "keep{t}" ON COMMIT DROP AS SELECT * FROM "{t}";')
    for t, condition in [('Users', admin_filter), ('UserRoles', f'"UserId"=\'{ADMIN}\''), ('RefreshTokens', f'"UserId"=\'{ADMIN}\'')]:
        statements.append(f'CREATE TEMP TABLE "keep{t}" ON COMMIT DROP AS SELECT * FROM "{t}" WHERE {condition};')
    statements.append('CREATE TEMP TABLE "keepAuditLogs" ON COMMIT DROP AS SELECT * FROM "AuditLogs";')
    metadata = { 'appointmentsDeleted': 'Appointments', 'doctorsDeleted': 'DoctorProfiles', 'patientsDeleted': 'PatientProfiles', 'dentalAssistantsDeleted': 'DentalAssistantProfiles' }
    pairs = [f"'{key}',(SELECT count(*) FROM \"{t}\")" for key,t in metadata.items()]
    for key, role in [('secretariesDeleted','SECRETARY'),('cashiersDeleted','CASHIER')]:
        pairs.append(f"'{key}',(SELECT count(*) FROM \"UserRoles\" ur JOIN \"Roles\" r ON r.\"Id\"=ur.\"RoleId\" WHERE r.\"Code\"='{role}' AND ur.\"UserId\" <> '{ADMIN}')")
    pairs.append(f"'usersDeleted',(SELECT count(*) FROM \"Users\" WHERE \"Id\" <> '{ADMIN}')")
    statements.append('CREATE TEMP TABLE reset_counts ON COMMIT DROP AS SELECT jsonb_build_object('+','.join(pairs)+') AS metadata;')
    for t in OPERATIONAL:
        statements.append(f'DELETE FROM "{t}";')
    statements += [f'UPDATE "RefreshTokens" SET "ReplacedByTokenId"=NULL WHERE "UserId" <> \'{ADMIN}\';',
                   f'DELETE FROM "RefreshTokens" WHERE "UserId" <> \'{ADMIN}\';',
                   f'DELETE FROM "UserRoles" WHERE "UserId" <> \'{ADMIN}\';']
    # Same explicit transformation on history and expected snapshot ensures exact verification.
    for t in ['AuditLogs','keepAuditLogs']:
        statements.append(f'UPDATE "{t}" SET "MetadataJson"=coalesce("MetadataJson",\'{{}}\'::jsonb)||jsonb_build_object(\'developmentResetActorId\',"UserId"), "UserId"=NULL WHERE "UserId" <> \'{ADMIN}\';')
    statements.append(f'DELETE FROM "Users" WHERE "Id" <> \'{ADMIN}\';')
    assertions = checks
    for t in OPERATIONAL:
        assertions += f'IF EXISTS(SELECT 1 FROM "{t}") THEN RAISE EXCEPTION \'Operational rows remain\'; END IF;'
    for t in PRESERVED + ['Users','UserRoles','RefreshTokens','AuditLogs']:
        assertions += f'IF EXISTS((SELECT * FROM "{t}" EXCEPT ALL SELECT * FROM "keep{t}") UNION ALL (SELECT * FROM "keep{t}" EXCEPT ALL SELECT * FROM "{t}")) THEN RAISE EXCEPTION \'Preservation mismatch: {t}\'; END IF;'
    statements += ['DO $$ BEGIN ' + assertions + ' END $$;',
                   f'INSERT INTO "AuditLogs" ("Id","UserId","Action","Entity","CreatedAt","MetadataJson") SELECT \'{reset_id}\',\'{ADMIN}\',\'development_test_data_reset\',\'Development\',now(),metadata FROM reset_counts;',
                   'SELECT metadata FROM reset_counts;', 'COMMIT;']
    result = sql('\n'.join(statements))
    report = {'before': counts, 'deleted': targets, 'resetAuditId': reset_id, 'transactionResult': result,
              'after': {t: int(sql(f'SELECT count(*) FROM "{t}";')) for t in TABLES}}
    (folder / f'test-reset-{stamp}.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report, indent=2))

if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
