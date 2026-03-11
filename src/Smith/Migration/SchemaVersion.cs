namespace Smith.Migration;

public enum SchemaVersion
{
    V1,
    V2
}

public static class SchemaDefinitions
{
    public const SchemaVersion LatestVersion = SchemaVersion.V2;

    public static string GetV1CreateSql() => """
        CREATE TABLE schema_migrations (
            version         INTEGER PRIMARY KEY,
            description     VARCHAR(255),
            script_name     VARCHAR(255),
            installed_on    TIMESTAMPTZ DEFAULT NOW(),
            execution_time_ms INTEGER,
            checksum        VARCHAR(64),
            success         BOOLEAN DEFAULT TRUE
        )
        """;

    public static string GetV2CreateSql() => """
        CREATE TABLE schema_migrations (
            version         INTEGER NOT NULL,
            script_type     VARCHAR(20) DEFAULT 'Migration',
            description     VARCHAR(255),
            script_name     VARCHAR(255),
            installed_on    TIMESTAMPTZ DEFAULT NOW(),
            execution_time_ms INTEGER,
            checksum        VARCHAR(64),
            success         BOOLEAN DEFAULT TRUE,
            PRIMARY KEY (version, script_type)
        )
        """;

    public static string GetV1ToV2UpgradeSql() => """
        DO $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns 
                WHERE table_name = 'schema_migrations' AND column_name = 'script_type'
            ) THEN
                ALTER TABLE schema_migrations ADD COLUMN script_type VARCHAR(20) DEFAULT 'Migration';
            END IF;
            
            IF EXISTS (
                SELECT 1 FROM information_schema.table_constraints 
                WHERE table_name = 'schema_migrations' 
                AND constraint_type = 'PRIMARY KEY' 
                AND constraint_name = 'schema_migrations_pkey'
            ) THEN
                EXECUTE 'ALTER TABLE schema_migrations DROP CONSTRAINT schema_migrations_pkey';
            END IF;
            
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.table_constraints 
                WHERE table_name = 'schema_migrations' 
                AND constraint_type = 'PRIMARY KEY'
            ) THEN
                EXECUTE 'ALTER TABLE schema_migrations ADD PRIMARY KEY (version, script_type)';
            END IF;
        END $$;
        """;
}
