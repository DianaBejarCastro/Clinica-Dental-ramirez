BEGIN READ ONLY;
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory";
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name;
SELECT conrelid::regclass AS table_name, conname, contype, pg_get_constraintdef(oid) AS definition
FROM pg_constraint WHERE connamespace = 'public'::regnamespace ORDER BY conrelid::regclass::text, conname;
SELECT tablename, indexname, indexdef FROM pg_indexes WHERE schemaname = 'public' ORDER BY tablename, indexname;
SELECT r."Code", count(rp."PermissionId") AS permissions FROM "Roles" r LEFT JOIN "RolePermissions" rp ON rp."RoleId" = r."Id" GROUP BY r."Code" ORDER BY r."Code";
SELECT count(*) AS permissions FROM "Permissions";
SELECT "Action", count(*) FROM "AuditLogs" GROUP BY "Action" ORDER BY "Action";
SELECT count(*) AS audit_with_secret_fields FROM "AuditLogs" WHERE "MetadataJson"::text ~* 'password|token|secret|credential';
COMMIT;
