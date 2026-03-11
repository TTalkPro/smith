using FluentAssertions;
using Smith.Database;

namespace Smith.Tests.Integration;

/// <summary>
/// PostgresSchemaInspector 集成测试：验证各类数据库对象的存在性检查
/// </summary>
public class PostgresSchemaInspectorTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task TableExists_ExistingTable_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE TABLE test_users (id SERIAL PRIMARY KEY)");

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.TableExistsAsync("test_users");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task TableExists_NonExistentTable_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.TableExistsAsync("nonexistent_table");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task FunctionExists_ExistingFunction_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, """
            CREATE FUNCTION test_func() RETURNS INTEGER AS $$
            BEGIN RETURN 42; END;
            $$ LANGUAGE plpgsql
            """);

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.FunctionExistsAsync("test_func");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task FunctionExists_NonExistentFunction_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.FunctionExistsAsync("nonexistent_func");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExtensionExists_AfterCreation_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE EXTENSION IF NOT EXISTS pgcrypto");

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.ExtensionExistsAsync("pgcrypto");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExtensionExists_NonExistent_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.ExtensionExistsAsync("nonexistent_ext");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task IndexExists_ExistingIndex_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE TABLE idx_test (id SERIAL PRIMARY KEY, name TEXT)");
        await ExecuteAsync(conn, "CREATE INDEX idx_test_name ON idx_test (name)");

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.IndexExistsAsync("idx_test_name");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task IndexExists_NonExistent_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.IndexExistsAsync("nonexistent_index");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task TriggerExists_ExistingTrigger_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE TABLE trigger_test (id SERIAL PRIMARY KEY, updated_at TIMESTAMPTZ)");
        await ExecuteAsync(conn, """
            CREATE FUNCTION trigger_test_func() RETURNS TRIGGER AS $$
            BEGIN NEW.updated_at = NOW(); RETURN NEW; END;
            $$ LANGUAGE plpgsql
            """);
        await ExecuteAsync(conn, "CREATE TRIGGER test_trigger BEFORE UPDATE ON trigger_test FOR EACH ROW EXECUTE FUNCTION trigger_test_func()");

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.TriggerExistsAsync("test_trigger");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerExists_NonExistent_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.TriggerExistsAsync("nonexistent_trigger");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ViewExists_ExistingView_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE TABLE view_source (id SERIAL PRIMARY KEY, active BOOLEAN)");
        await ExecuteAsync(conn, "CREATE VIEW active_items AS SELECT * FROM view_source WHERE active = true");

        var inspector = new PostgresSchemaInspector(conn);
        var exists = await inspector.ViewExistsAsync("active_items");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ViewExists_NonExistent_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var exists = await inspector.ViewExistsAsync("nonexistent_view");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ObjectExists_DispatchesCorrectly()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE TABLE dispatch_test (id SERIAL PRIMARY KEY)");
        await ExecuteAsync(conn, "CREATE INDEX idx_dispatch ON dispatch_test (id)");

        var inspector = new PostgresSchemaInspector(conn);

        var tableObj = new DatabaseObject(DatabaseObjectType.Table, "dispatch_test");
        var indexObj = new DatabaseObject(DatabaseObjectType.Index, "idx_dispatch");
        var missingObj = new DatabaseObject(DatabaseObjectType.Table, "no_such_table");

        (await inspector.ObjectExistsAsync(tableObj)).Should().BeTrue();
        (await inspector.ObjectExistsAsync(indexObj)).Should().BeTrue();
        (await inspector.ObjectExistsAsync(missingObj)).Should().BeFalse();
    }

    [Fact]
    public async Task GetTableCount_ReturnsCorrectCount()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var inspector = new PostgresSchemaInspector(conn);

        var countBefore = await inspector.GetTableCountAsync();
        await ExecuteAsync(conn, "CREATE TABLE count_test_a (id INT)");
        await ExecuteAsync(conn, "CREATE TABLE count_test_b (id INT)");
        var countAfter = await inspector.GetTableCountAsync();

        countAfter.Should().Be(countBefore + 2);
    }

    [Fact]
    public async Task TableExists_WithSchema_ChecksCorrectSchema()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        await ExecuteAsync(conn, "CREATE SCHEMA IF NOT EXISTS custom_schema");
        await ExecuteAsync(conn, "CREATE TABLE custom_schema.schema_test (id INT)");

        var inspector = new PostgresSchemaInspector(conn);

        (await inspector.TableExistsAsync("schema_test", "custom_schema")).Should().BeTrue();
        (await inspector.TableExistsAsync("schema_test", "public")).Should().BeFalse();
    }

    private static async Task ExecuteAsync(Npgsql.NpgsqlConnection conn, string sql)
    {
        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
