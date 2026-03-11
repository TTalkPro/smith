using Spectre.Console.Cli;
using Smith.Configuration;

namespace Smith.Commands.Settings;

public class ConnectionSettings : CommandSettings
{
    [CommandOption("-d|--database")]
    public string? Database { get; set; }

    [CommandOption("-H|--host")]
    public string? Host { get; set; }

    [CommandOption("-P|--port")]
    public int? Port { get; set; }

    [CommandOption("-u|--user")]
    public string? User { get; set; }

    [CommandOption("-p|--password")]
    public string? Password { get; set; }

    [CommandOption("-D|--database-path")]
    public string? DatabasePath { get; set; }

    [CommandOption("-v|--verbose")]
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
