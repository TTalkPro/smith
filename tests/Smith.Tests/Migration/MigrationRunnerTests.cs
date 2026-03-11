using FluentAssertions;
using Moq;
using Smith.Migration;
using Smith.Rendering;

namespace Smith.Tests.Migration;

public class MigrationRunnerTests
{
    private readonly Mock<IMigrationTracker> _trackerMock = new();
    private readonly Mock<IConsoleRenderer> _rendererMock = new();

    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void LoadFromDirectory_ReturnsCorrectMigrations()
    {
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);

        migrations.Should().HaveCount(3);
        migrations[0].Version.Should().Be(1);
        migrations[1].Version.Should().Be(2);
        migrations[2].Version.Should().Be(3);
    }

    [Fact]
    public void PendingMigrations_FiltersByCurrentVersion()
    {
        var allMigrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var currentVersion = 1;

        var pending = allMigrations
            .Where(m => m.Version > currentVersion)
            .ToList();

        pending.Should().HaveCount(2);
        pending[0].Version.Should().Be(2);
        pending[1].Version.Should().Be(3);
    }

    [Fact]
    public void PendingMigrations_WithTargetVersion_LimitsScope()
    {
        var allMigrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var currentVersion = 0;
        var targetVersion = 2;

        var pending = allMigrations
            .Where(m => m.Version > currentVersion)
            .Where(m => m.Version <= targetVersion)
            .ToList();

        pending.Should().HaveCount(2);
        pending[0].Version.Should().Be(1);
        pending[1].Version.Should().Be(2);
    }

    [Fact]
    public void PendingMigrations_AllApplied_ReturnsEmpty()
    {
        var allMigrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var currentVersion = 3;

        var pending = allMigrations
            .Where(m => m.Version > currentVersion)
            .ToList();

        pending.Should().BeEmpty();
    }
}
