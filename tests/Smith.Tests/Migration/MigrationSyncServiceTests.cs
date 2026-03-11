using FluentAssertions;
using Moq;
using Smith.Database;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Migration;

public class MigrationSyncServiceTests
{
    private readonly Mock<IMigrationTracker> _trackerMock = new();
    private readonly Mock<ISchemaInspector> _inspectorMock = new();
    private readonly Mock<IConsoleRenderer> _rendererMock = new();
    private readonly MigrationSyncService _service;

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public MigrationSyncServiceTests()
    {
        _service = new MigrationSyncService(_trackerMock.Object, _inspectorMock.Object, _rendererMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_AllObjectsExist_MarksAsSynced()
    {
        _inspectorMock
            .Setup(x => x.ObjectExistsAsync(It.IsAny<DatabaseObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        // Reason: 使用第二个 fixture 因为它有 CREATE TABLE 和 CREATE INDEX
        var pending = new List<MigrationFile> { migrations[1] };

        var result = await _service.AnalyzeAsync(pending);

        result.Synced.Should().HaveCount(1);
        result.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_SomeObjectsMissing_MarksAsSkipped()
    {
        _inspectorMock
            .Setup(x => x.ObjectExistsAsync(
                It.Is<DatabaseObject>(o => o.Type == DatabaseObjectType.Table),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _inspectorMock
            .Setup(x => x.ObjectExistsAsync(
                It.Is<DatabaseObject>(o => o.Type == DatabaseObjectType.Index),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var pending = new List<MigrationFile> { migrations[1] };

        var result = await _service.AnalyzeAsync(pending);

        result.Synced.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].Missing.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_NoDetectableObjects_MarksAsSkipped()
    {
        // Reason: fixture 003 只有 ALTER TABLE，SqlObjectDetector 无法检测 ALTER 语句创建的对象
        // 但它也有 CREATE INDEX，所以我们使用一个没有任何 CREATE 的 SQL
        var tempDir = Path.Combine(Path.GetTempPath(), $"smith_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var sqlPath = Path.Combine(tempDir, "100_alter_only.sql");
        await File.WriteAllTextAsync(sqlPath, "ALTER TABLE users ADD COLUMN bio TEXT;");

        try
        {
            var migration = MigrationFile.Parse(sqlPath)!;
            var result = await _service.AnalyzeAsync([migration]);

            result.Synced.Should().BeEmpty();
            result.Skipped.Should().HaveCount(1);
            result.Skipped[0].Missing.Should().Contain("无可检测的数据库对象");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ApplyAsync_RecordsAllMigrations()
    {
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var synced = new List<SyncedMigration>
        {
            new(migrations[0], [new DatabaseObject(DatabaseObjectType.Extension, "uuid-ossp")]),
            new(migrations[1], [new DatabaseObject(DatabaseObjectType.Table, "users")])
        };

        await _service.ApplyAsync(synced);

        _trackerMock.Verify(
            x => x.RecordAsync(It.IsAny<MigrationFile>(), 0, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
