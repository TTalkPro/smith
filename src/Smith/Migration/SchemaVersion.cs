namespace Smith.Migration;

/// <summary>
/// schema_migrations 表的 Schema 版本号
/// </summary>
public enum SchemaVersion
{
    /// <summary>V1: 单列主键 (version)</summary>
    V1,

    /// <summary>V2: 复合主键 (version, script_type)，支持脚本类型区分</summary>
    V2
}

/// <summary>
/// Schema 定义：包含各版本的建表 SQL 和版本间的升级 SQL
/// </summary>
public static class SchemaDefinitions
{
    /// <summary>当前最新 Schema 版本</summary>
    public const SchemaVersion LatestVersion = SchemaVersion.V2;

    /// <summary>V1 版本建表 SQL（单列主键）</summary>
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

    /// <summary>V2 版本建表 SQL（复合主键，含 script_type）</summary>
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

    /// <summary>V1 → V2 升级 SQL：添加 script_type 列并修改主键</summary>
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
