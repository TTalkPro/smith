namespace Smith.Migration;

/// <summary>
/// SQLite 是新增支持，schema_migrations 表始终以 V2 格式创建，无需升级
/// </summary>
public class SqliteSchemaDetector : ISchemaDetector
{
    public Task<SchemaVersion> DetectCurrentVersionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(SchemaVersion.V2);
    }

    public Task<SchemaDiff> GetDiffAsync(SchemaVersion targetVersion, CancellationToken ct = default)
    {
        return Task.FromResult(new SchemaDiff(SchemaVersion.V2, targetVersion, []));
    }

    public Task<bool> NeedsUpgradeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }
}
