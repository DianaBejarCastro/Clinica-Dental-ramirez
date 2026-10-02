BEGIN READ ONLY;
\dt
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT t.relname AS table_name, c.conname, c.contype, pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c JOIN pg_class t ON c.conrelid = t.oid
WHERE t.relname IN ('Clinics','Centers','DoctorProfiles','Specialties','DoctorSpecialties','DoctorCenters','DoctorSchedules')
ORDER BY t.relname,c.contype,c.conname;
SELECT tablename,indexname,indexdef FROM pg_indexes
WHERE schemaname='public' AND tablename IN ('Clinics','Centers','DoctorProfiles','Specialties','DoctorSpecialties','DoctorCenters','DoctorSchedules')
ORDER BY tablename,indexname;
SELECT table_name,column_name,is_nullable,data_type FROM information_schema.columns
WHERE table_schema='public' AND table_name IN ('Clinics','Centers','DoctorProfiles','Specialties','DoctorSpecialties','DoctorCenters','DoctorSchedules')
ORDER BY table_name,ordinal_position;
SELECT count(*) AS clinic_count FROM "Clinics";
SELECT "Code","Name","IsActive" FROM "Specialties" ORDER BY "Code";
SELECT "Action",count(*) FROM "AuditLogs"
WHERE "Action" LIKE 'clinic_%' OR "Action" LIKE 'center_%' OR "Action" LIKE 'doctor_%' OR "Action" LIKE 'specialty_%' OR "Action" LIKE 'schedule_%'
GROUP BY "Action" ORDER BY "Action";
SELECT count(*) AS auxiliary_metadata_with_sensitive_keys FROM "AuditLogs"
WHERE "MetadataJson"::text ~* 'password|passwordhash|access_token|refresh_token|jwt_secret|credential';
DO $$
BEGIN
    IF (SELECT count(*) FROM "Clinics") <> 1 THEN RAISE EXCEPTION 'Se esperaba una sola clinica'; END IF;
    IF (SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN
        ('Clinics','Centers','DoctorProfiles','Specialties','DoctorSpecialties','DoctorCenters','DoctorSchedules')) <> 7
    THEN RAISE EXCEPTION 'Faltan tablas administrativas'; END IF;
    IF (SELECT count(*) FROM "Specialties" WHERE "Code" IN ('GENERAL_DENTISTRY','ORTHODONTICS',
        'PEDIATRIC_DENTISTRY','ENDODONTICS','PERIODONTICS','ORAL_MAXILLOFACIAL_SURGERY','PROSTHODONTICS',
        'ORAL_PATHOLOGY','ORAL_MAXILLOFACIAL_RADIOLOGY','PUBLIC_HEALTH_DENTISTRY','IMPLANTOLOGY','AESTHETIC_DENTISTRY')) <> 12
    THEN RAISE EXCEPTION 'Falta catalogo inicial de especialidades'; END IF;
    IF EXISTS (SELECT 1 FROM "Centers" WHERE "IsMain" AND "IsActive" GROUP BY "ClinicId" HAVING count(*) > 1)
    THEN RAISE EXCEPTION 'Mas de una sucursal principal activa'; END IF;
    IF EXISTS (SELECT 1 FROM "DoctorProfiles" d WHERE NOT EXISTS (
        SELECT 1 FROM "UserRoles" ur JOIN "Roles" r ON r."Id"=ur."RoleId"
        WHERE ur."UserId"=d."UserId" AND r."Code"='DOCTOR'))
    THEN RAISE EXCEPTION 'Perfil sin rol DOCTOR'; END IF;
    IF EXISTS (SELECT 1 FROM "DoctorSchedules" s
        JOIN "DoctorCenters" dc ON dc."DoctorId"=s."DoctorId" AND dc."CenterId"=s."CenterId"
        JOIN "DoctorProfiles" d ON d."Id"=s."DoctorId" JOIN "Centers" c ON c."Id"=s."CenterId"
        WHERE s."IsActive" AND (NOT dc."IsActive" OR NOT d."IsActive" OR NOT c."IsActive"))
    THEN RAISE EXCEPTION 'Horario activo en relacion inactiva'; END IF;
    IF EXISTS (SELECT 1 FROM "DoctorSchedules" a JOIN "DoctorSchedules" b
        ON a."DoctorId"=b."DoctorId" AND a."DayOfWeek"=b."DayOfWeek" AND a."Id"<b."Id"
        WHERE a."IsActive" AND b."IsActive" AND a."StartTime"<b."EndTime" AND b."StartTime"<a."EndTime")
    THEN RAISE EXCEPTION 'Solapamiento de horarios activos'; END IF;
END $$;
COMMIT;
