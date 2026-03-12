using System.Data.Common;
using Smith.Rendering;

namespace Smith.Migration;

/// <summary>
/// 种子数据执行器，统一管理种子文件的加载、校验和执行。
/// 所有种子文件必须符合 Sxxx_name.sql 命名规范。
/// </summary>
public static class SeedRunner
{
    /// <summary>
    /// 加载并执行指定目录下的种子数据文件
    /// </summary>
    /// <param name="connection">数据库连接</param>
    /// <param name="seedsPath">种子文件目录路径</param>
    /// <param name="label">操作标签（用于输出，如"必需种子数据"）</param>
    /// <param name="renderer">控制台渲染器</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>成功执行的文件数，命名不规范时返回 -1</returns>
    public static async Task<int> ExecuteAsync(
        DbConnection connection, string seedsPath, string label,
        IConsoleRenderer renderer, CancellationToken ct = default)
    {
        if (!Directory.Exists(seedsPath))
        {
            renderer.Warning($"目录不存在: {seedsPath}");
            return 0;
        }

        var (seedFiles, invalidFiles) = LoadAndValidate(seedsPath);

        if (invalidFiles.Count > 0)
        {
            renderer.Error("以下文件不符合命名规范 (应为 Sxxx_name.sql，如 S001_roles.sql):");
            foreach (var file in invalidFiles)
                renderer.Warning($"  {file}");
            return -1;
        }

        if (seedFiles.Count == 0)
        {
            renderer.Info($"没有{label}文件");
            return 0;
        }

        renderer.Info($"执行 {seedFiles.Count} 个{label}文件...");
        foreach (var seed in seedFiles)
        {
            await ExecuteSqlFileAsync(connection, seed.FilePath, ct);
            renderer.Success($"  {seed.FileName}");
        }

        return seedFiles.Count;
    }

    /// <summary>
    /// 加载种子文件并校验命名规范，返回合法文件和不合法文件名
    /// </summary>
    private static (List<MigrationFile> valid, List<string?> invalid) LoadAndValidate(string seedsPath)
    {
        var allSqlFiles = Directory.GetFiles(seedsPath, "*.sql");
        var seedFiles = MigrationFile.LoadFromDirectory(seedsPath)
            .Where(f => f.Type == ScriptType.SeedRequired)
            .ToList();

        var invalidFiles = allSqlFiles
            .Where(f => seedFiles.All(s => s.FilePath != f))
            .Select(Path.GetFileName)
            .ToList();

        return (seedFiles, invalidFiles);
    }

    /// <summary>
    /// 执行单个 SQL 文件
    /// </summary>
    public static async Task ExecuteSqlFileAsync(
        DbConnection connection, string filePath, CancellationToken ct = default)
    {
        var sql = await File.ReadAllTextAsync(filePath, ct);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
