using Npgsql;
using Smith.Rendering;

namespace Smith.Migration;

public class SchemaUpgrader
{
    private readonly NpgsqlConnection _connection;
    private readonly IConsoleRenderer _renderer;

    public SchemaUpgrader(NpgsqlConnection connection, IConsoleRenderer renderer)
    {
        _connection = connection;
        _renderer = renderer;
    }

    public async Task<UpgradeResult> UpgradeAsync(
        SchemaVersion fromVersion,
        SchemaVersion toVersion,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        if (fromVersion >= toVersion)
        {
            return new UpgradeResult(false, "Schema 已是最新版本，无需升级", null);
        }

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

            await using var cmd = new NpgsqlCommand(sql, _connection, transaction);
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

    private string GenerateUpgradeSql(SchemaVersion from, SchemaVersion to)
    {
        if (from == SchemaVersion.V1 && to == SchemaVersion.V2)
        {
            return SchemaDefinitions.GetV1ToV2UpgradeSql();
        }

        throw new NotSupportedException($"不支持从 {from} 升级到 {to}");
    }
}

public record UpgradeResult(
    bool Success,
    string Message,
    string? ExecutedSql);
