using FluentAssertions;
using Smith.Migration;

namespace Smith.Tests.Integration;

/// <summary>
/// SchemaDetector 集成测试：验证 schema 版本检测功能
/// </summary>
public class SchemaDetectorTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task DetectCurrentVersion_NoTable_ReturnsV1()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var detector = new SchemaDetector(conn);

        var version = await detector.DetectCurrentVersionAsync();

        // 没有表时，默认返回 V1
        version.Should().Be(SchemaVersion.V1);
    }

    [Fact]
    public async Task DetectCurrentVersion_V1Schema_ReturnsV1()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V1 版本的表（单列主键，无 script_type）
        await using (var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY,
                description VARCHAR(255),
                script_name VARCHAR(255),
                installed_on TIMESTAMPTZ DEFAULT NOW(),
                execution_time_ms INTEGER,
                checksum VARCHAR(64),
                success BOOLEAN DEFAULT TRUE
            )
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var version = await detector.DetectCurrentVersionAsync();

        version.Should().Be(SchemaVersion.V1);
    }

    [Fact]
    public async Task DetectCurrentVersion_V2Schema_ReturnsV2()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V2 版本的表（复合主键，有 script_type）
        await using (var cmd = new Npgsql.NpgsqlCommand("""
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
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var version = await detector.DetectCurrentVersionAsync();

        version.Should().Be(SchemaVersion.V2);
    }

    [Fact]
    public async Task NeedsUpgrade_NoTable_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        var detector = new SchemaDetector(conn);

        var needsUpgrade = await detector.NeedsUpgradeAsync();

        // 没有表时不需要升级（新安装会直接创建 V2）
        needsUpgrade.Should().BeFalse();
    }

    [Fact]
    public async Task NeedsUpgrade_V1Schema_ReturnsTrue()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V1 版本的表
        await using (var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY,
                description VARCHAR(255)
            )
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var needsUpgrade = await detector.NeedsUpgradeAsync();

        needsUpgrade.Should().BeTrue();
    }

    [Fact]
    public async Task NeedsUpgrade_V2Schema_ReturnsFalse()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V2 版本的表
        await using (var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER NOT NULL,
                script_type VARCHAR(20) DEFAULT 'Migration',
                description VARCHAR(255),
                PRIMARY KEY (version, script_type)
            )
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var needsUpgrade = await detector.NeedsUpgradeAsync();

        needsUpgrade.Should().BeFalse();
    }

    [Fact]
    public async Task GetDiff_SameVersion_ReturnsEmptyChanges()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V2 版本的表
        await using (var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER NOT NULL,
                script_type VARCHAR(20) DEFAULT 'Migration',
                PRIMARY KEY (version, script_type)
            )
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var diff = await detector.GetDiffAsync(SchemaVersion.V2);

        diff.CurrentVersion.Should().Be(SchemaVersion.V2);
        diff.TargetVersion.Should().Be(SchemaVersion.V2);
        diff.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDiff_V1ToV2_ReturnsExpectedChanges()
    {
        await using var conn = await _fixture.OpenConnectionAsync();
        
        // 创建 V1 版本的表
        await using (var cmd = new Npgsql.NpgsqlCommand("""
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY,
                description VARCHAR(255)
            )
            """, conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        var detector = new SchemaDetector(conn);
        var diff = await detector.GetDiffAsync(SchemaVersion.V2);

        diff.CurrentVersion.Should().Be(SchemaVersion.V1);
        diff.TargetVersion.Should().Be(SchemaVersion.V2);
        diff.Changes.Should().HaveCount(2);
        diff.Changes.Should().Contain(c => c.Contains("script_type"));
        diff.Changes.Should().Contain(c => c.Contains("主键"));
    }
}
