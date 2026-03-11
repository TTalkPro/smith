using FluentAssertions;
using Smith.Configuration;

namespace Smith.Tests.Configuration;

public class ConfigLoaderTests
{
    [Fact]
    public void Load_WithDefaults_ReturnsDefaultValues()
    {
        var config = ConfigLoader.Load();

        config.Host.Should().Be("localhost");
        config.Port.Should().Be(5432);
        config.User.Should().Be("postgres");
        config.Password.Should().BeNull();
        config.Database.Should().BeNull();
        config.Verbose.Should().BeFalse();
    }

    [Fact]
    public void Load_WithCliParams_CliTakesPriority()
    {
        var config = ConfigLoader.Load(
            cliHost: "db.example.com",
            cliPort: 5433,
            cliUser: "myuser",
            cliPassword: "mypass",
            cliDatabase: "mydb",
            cliDatabasePath: "/tmp/db",
            cliVerbose: true
        );

        config.Host.Should().Be("db.example.com");
        config.Port.Should().Be(5433);
        config.User.Should().Be("myuser");
        config.Password.Should().Be("mypass");
        config.Database.Should().Be("mydb");
        config.DatabasePath.Should().Be("/tmp/db");
        config.Verbose.Should().BeTrue();
    }

    [Fact]
    public void Load_WithEnvVars_EnvVarsTakePriority()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SMITH_HOST", "env-host");
        Environment.SetEnvironmentVariable("SMITH_PORT", "5434");
        Environment.SetEnvironmentVariable("SMITH_USER", "envuser");
        Environment.SetEnvironmentVariable("SMITH_PASSWORD", "envpass");
        Environment.SetEnvironmentVariable("SMITH_DATABASE", "envdb");
        Environment.SetEnvironmentVariable("SMITH_DATABASE_PATH", "/env/path");

        try
        {
            var config = ConfigLoader.Load();

            config.Host.Should().Be("env-host");
            config.Port.Should().Be(5434);
            config.User.Should().Be("envuser");
            config.Password.Should().Be("envpass");
            config.Database.Should().Be("envdb");
            config.DatabasePath.Should().Be("/env/path");
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SMITH_HOST", null);
            Environment.SetEnvironmentVariable("SMITH_PORT", null);
            Environment.SetEnvironmentVariable("SMITH_USER", null);
            Environment.SetEnvironmentVariable("SMITH_PASSWORD", null);
            Environment.SetEnvironmentVariable("SMITH_DATABASE", null);
            Environment.SetEnvironmentVariable("SMITH_DATABASE_PATH", null);
        }
    }

    [Fact]
    public void Load_CliOverridesEnv()
    {
        Environment.SetEnvironmentVariable("SMITH_HOST", "env-host");
        Environment.SetEnvironmentVariable("SMITH_DATABASE", "envdb");

        try
        {
            var config = ConfigLoader.Load(
                cliHost: "cli-host",
                cliDatabase: "clidb"
            );

            config.Host.Should().Be("cli-host");
            config.Database.Should().Be("clidb");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SMITH_HOST", null);
            Environment.SetEnvironmentVariable("SMITH_DATABASE", null);
        }
    }

    [Fact]
    public void Load_InvalidEnvPort_FallsToDefault()
    {
        Environment.SetEnvironmentVariable("SMITH_PORT", "not-a-number");

        try
        {
            var config = ConfigLoader.Load();
            config.Port.Should().Be(5432);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SMITH_PORT", null);
        }
    }
}

public class SmithConfigTests
{
    [Fact]
    public void GetConnectionString_WithDatabase_ReturnsValidString()
    {
        var config = new SmithConfig
        {
            Host = "localhost",
            Port = 5432,
            User = "postgres",
            Database = "testdb"
        };

        var connStr = config.GetConnectionString();

        connStr.Should().Contain("Host=localhost");
        connStr.Should().Contain("Database=testdb");
        connStr.Should().Contain("Username=postgres");
    }

    [Fact]
    public void GetConnectionString_WithoutDatabase_ThrowsException()
    {
        var config = new SmithConfig();

        var act = () => config.GetConnectionString();
        act.Should().Throw<InvalidOperationException>().WithMessage("*未指定*");
    }

    [Fact]
    public void GetAdminConnectionString_ConnectsToPostgres()
    {
        var config = new SmithConfig
        {
            Host = "localhost",
            Database = "mydb"
        };

        var connStr = config.GetAdminConnectionString();
        connStr.Should().Contain("Database=postgres");
        connStr.Should().NotContain("Database=mydb");
    }

    [Fact]
    public void GetMigrationsPath_WithDatabasePath_ReturnsCorrectPath()
    {
        var config = new SmithConfig { DatabasePath = "/data/owl" };
        config.GetMigrationsPath().Should().Be(Path.Combine("/data/owl", "migrations"));
    }

    [Fact]
    public void GetSeedsPath_ReturnsCorrectPath()
    {
        var config = new SmithConfig { DatabasePath = "/data/owl" };
        config.GetSeedsPath("required").Should().Be(Path.Combine("/data/owl", "seeds", "required"));
        config.GetSeedsPath("examples").Should().Be(Path.Combine("/data/owl", "seeds", "examples"));
    }

    [Theory]
    [InlineData("owl_dev")]
    [InlineData("owl_test")]
    [InlineData("mydb123")]
    [InlineData("_private")]
    public void ValidateDatabaseName_ValidNames_DoesNotThrow(string name)
    {
        var act = () => SmithConfig.ValidateDatabaseName(name);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("owl-dev")]
    [InlineData("my db")]
    [InlineData("db;DROP TABLE")]
    [InlineData("123start")]
    [InlineData("")]
    public void ValidateDatabaseName_InvalidNames_Throws(string name)
    {
        var act = () => SmithConfig.ValidateDatabaseName(name);
        act.Should().Throw<ArgumentException>();
    }
}
