using System.Data.Common;
using Smith.Rendering;

namespace Smith.Migration;

/// <summary>
/// Schema 升级器：在事务中执行 schema_migrations 表的结构升级
/// </summary>
public class SchemaUpgrader
{
    private readonly DbConnection _connection;
    private readonly IConsoleRenderer _renderer;

    public SchemaUpgrader(DbConnection connection, IConsoleRenderer renderer)
    {
        _connection = connection;
        _renderer = renderer;
    }

    /// <summary>
    /// 执行从指定版本到目标版本的 Schema 升级
    /// </summary>
    public async Task<UpgradeResult> UpgradeAsync(
        SchemaVersion fromVersion,
        SchemaVersion toVersion,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        if (fromVersion >= toVersion)
            return new UpgradeResult(false, "Schema 已是最新版本，无需升级", null);

        var sql = GenerateUpgradeSql(fromVersion, toVersion);

        if (dryRun)
        {
            _renderer.Info("预览模式 - 将执行以下 SQL:");
            _renderer.Info(sql);
            return new UpgradeResult(false, "Dry-run 模式，未执行升级", sql);
        }

        await using var transaction = await _connection.BeginTransactionAsync(ct);
        try
        {
            _renderer.Info("开始升级 Schema...");
            _renderer.Info($"从版本 {fromVersion} 到版本 {toVersion}");

            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            _renderer.Success("Schema 升级完成");
            return new UpgradeResult(true, "升级成功", sql);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _renderer.Error($"升级失败: {ex.Message}");
            return new UpgradeResult(false, $"升级失败: {ex.Message}", sql);
        }
    }

    /// <summary>
    /// 根据版本范围生成对应的升级 SQL
    /// </summary>
    private static string GenerateUpgradeSql(SchemaVersion from, SchemaVersion to)
    {
        if (from == SchemaVersion.V1 && to == SchemaVersion.V2)
            return SchemaDefinitions.GetV1ToV2UpgradeSql();

        throw new NotSupportedException($"不支持从 {from} 升级到 {to}");
    }
}

/// <summary>
/// Schema 升级结果模型
/// </summary>
public record UpgradeResult(
    bool Success,
    string Message,
    string? ExecutedSql);
