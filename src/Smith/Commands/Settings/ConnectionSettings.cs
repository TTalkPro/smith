using Smith.Configuration;

namespace Smith.Commands.Settings;

/// <summary>
/// 数据库连接参数模型，用于从 CLI 选项收集连接信息
/// </summary>
public class ConnectionSettings
{
    public string? Database { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? DatabasePath { get; set; }
    public bool Verbose { get; set; }

    /// <summary>
    /// 将连接参数转换为 SmithConfig 配置对象
    /// </summary>
    public SmithConfig BuildConfig()
    {
        return ConfigLoader.Load(
            cliHost: Host,
            cliPort: Port,
            cliUser: User,
            cliPassword: Password,
            cliDatabase: Database,
            cliDatabasePath: DatabasePath,
            cliVerbose: Verbose
        );
    }
}
