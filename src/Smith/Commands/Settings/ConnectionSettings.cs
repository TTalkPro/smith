using Smith.Configuration;

namespace Smith.Commands.Settings;

public class ConnectionSettings
{
    public string? Database { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? DatabasePath { get; set; }
    public bool Verbose { get; set; }

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
