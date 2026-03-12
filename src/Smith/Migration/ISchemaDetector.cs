namespace Smith.Migration;

/// <summary>
/// Schema 版本检测接口：检测 schema_migrations 表的当前版本和升级需求
/// </summary>
public interface ISchemaDetector
{
    /// <summary>检测当前 Schema 版本</summary>
    Task<SchemaVersion> DetectCurrentVersionAsync(CancellationToken ct = default);

    /// <summary>获取当前版本与目标版本之间的差异</summary>
    Task<SchemaDiff> GetDiffAsync(SchemaVersion targetVersion, CancellationToken ct = default);

    /// <summary>检查是否需要升级 Schema</summary>
    Task<bool> NeedsUpgradeAsync(CancellationToken ct = default);
}

/// <summary>
/// Schema 差异模型：描述当前版本到目标版本的变更内容
/// </summary>
public record SchemaDiff(
    SchemaVersion CurrentVersion,
    SchemaVersion TargetVersion,
    List<string> Changes);
