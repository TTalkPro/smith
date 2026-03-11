using FluentAssertions;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Integration;

public class SchemaUpgraderTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task UpgradeAsync_AlreadyLatestVersion_ReturnsNoUpgradeNeeded()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV2TableAsync(conn);
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        var result = await upgrader.UpgradeAsync(SchemaVersion.V2, SchemaVersion.V2);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("已是最新版本");
    }

    [Fact]
    public async Task UpgradeAsync_DryRun_DoesNotModifySchema()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        var result = await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2, dryRun: true);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Dry-run");
        result.ExecutedSql.Should().NotBeNullOrEmpty();

        var hasScriptType = await HasColumnAsync(conn, "script_type");
        hasScriptType.Should().BeFalse("dry-run 不应修改表结构");
    }

    [Fact]
    public async Task UpgradeAsync_V1ToV2_AddsScriptTypeColumn()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        await InsertV1DataAsync(conn, 1, "test migration");
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        var result = await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2, dryRun: false);

        result.Success.Should().BeTrue();
        
        var hasScriptType = await HasColumnAsync(conn, "script_type");
        hasScriptType.Should().BeTrue();
    }

    [Fact]
    public async Task UpgradeAsync_V1ToV2_ChangesPrimaryKey()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2);

        var pkColumnCount = await GetPrimaryKeyColumnCountAsync(conn);
        pkColumnCount.Should().Be(2, "主键应变为复合主键 (version, script_type)");
    }

    [Fact]
    public async Task UpgradeAsync_V1ToV2_PreservesData()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        await InsertV1DataAsync(conn, 1, "first migration");
        await InsertV1DataAsync(conn, 2, "second migration");
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        var result = await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2);

        result.Success.Should().BeTrue();

        var count = await GetDataCountAsync(conn);
        count.Should().Be(2, "升级不应丢失数据");
    }

    [Fact]
    public async Task UpgradeAsync_V1ToV2_SetsDefaultScriptType()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        await InsertV1DataAsync(conn, 1, "test");
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2);

        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT script_type FROM schema_migrations WHERE version = 1", conn);
        var scriptType = (string?)await cmd.ExecuteScalarAsync();
        scriptType.Should().Be("Migration");
    }

    [Fact]
    public async Task UpgradeAsync_Idempotent()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var renderer = new TerminalGuiRenderer();
        
        await CreateV1TableAsync(conn);
        
        var upgrader = new SchemaUpgrader(conn, renderer);
        
        var result1 = await upgrader.UpgradeAsync(SchemaVersion.V1, SchemaVersion.V2);
        result1.Success.Should().BeTrue();

        var result2 = await upgrader.UpgradeAsync(SchemaVersion.V2, SchemaVersion.V2);
        result2.Success.Should().BeFalse();
        result2.Message.Should().Contain("已是最新版本");
    }

    private static async Task CreateV1TableAsync(Npgsql.NpgsqlConnection conn)
    {
        await using var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY,
                description VARCHAR(255),
                script_name VARCHAR(255),
                installed_on TIMESTAMPTZ DEFAULT NOW(),
                execution_time_ms INTEGER,
                checksum VARCHAR(64),
                success BOOLEAN DEFAULT TRUE
            )
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateV2TableAsync(Npgsql.NpgsqlConnection conn)
    {
        await using var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER NOT NULL,
                script_type VARCHAR(20) DEFAULT 'Migration',
                description VARCHAR(255),
                script_name VARCHAR(255),
                installed_on TIMESTAMPTZ DEFAULT NOW(),
                execution_time_ms INTEGER,
                checksum VARCHAR(64),
                success BOOLEAN DEFAULT TRUE,
                PRIMARY KEY (version, script_type)
            )
            """, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertV1DataAsync(Npgsql.NpgsqlConnection conn, int version, string description)
    {
        await using var cmd = new Npgsql.NpgsqlCommand(
            "INSERT INTO schema_migrations (version, description) VALUES ($1, $2)", conn);
        cmd.Parameters.AddWithValue(version);
        cmd.Parameters.AddWithValue(description);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<bool> HasColumnAsync(Npgsql.NpgsqlConnection conn, string columnName)
    {
        await using var cmd = new Npgsql.NpgsqlCommand($"""
            SELECT COUNT(*) FROM information_schema.columns 
            WHERE table_name = 'schema_migrations' AND column_name = '{columnName}'
            """, conn);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    private static async Task<int> GetPrimaryKeyColumnCountAsync(Npgsql.NpgsqlConnection conn)
    {
        await using var cmd = new Npgsql.NpgsqlCommand("""
            SELECT COUNT(*) FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu 
                ON tc.constraint_name = kcu.constraint_name
            WHERE tc.table_name = 'schema_migrations'
            AND tc.constraint_type = 'PRIMARY KEY'
            """, conn);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return (int)count;
    }

    private static async Task<long> GetDataCountAsync(Npgsql.NpgsqlConnection conn)
    {
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT COUNT(*) FROM schema_migrations", conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
