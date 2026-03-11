using FluentAssertions;
using Smith.Migration;

namespace Smith.Tests.Migration;

public class MigrationFileTests
{
    private string FixturesPath => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void Parse_ValidFileName_ExtractsVersionAndName()
    {
        var filePath = Path.Combine(FixturesPath, "001_create_extensions.sql");
        var migration = MigrationFile.Parse(filePath);

        migration.Should().NotBeNull();
        migration!.Version.Should().Be(1);
        migration.Name.Should().Be("create_extensions");
        migration.FileName.Should().Be("001_create_extensions.sql");
    }

    [Fact]
    public void Parse_ValidFileName_GeneratesDescription()
    {
        var filePath = Path.Combine(FixturesPath, "002_create_users_table.sql");
        var migration = MigrationFile.Parse(filePath);

        migration.Should().NotBeNull();
        migration!.Description.Should().Be("Create users table");
    }

    [Fact]
    public void Parse_ThreeDigitVersion_ExtractsCorrectly()
    {
        var filePath = "/tmp/029_single_user_migration.sql";
        var migration = MigrationFile.Parse(filePath);

        migration.Should().NotBeNull();
        migration!.Version.Should().Be(29);
        migration.Name.Should().Be("single_user_migration");
        migration.Description.Should().Be("Single user migration");
    }

    [Fact]
    public void Parse_InvalidFileName_ReturnsNull()
    {
        MigrationFile.Parse("/tmp/readme.md").Should().BeNull();
        MigrationFile.Parse("/tmp/no_version.sql").Should().BeNull();
        MigrationFile.Parse("/tmp/.hidden.sql").Should().BeNull();
    }

    [Fact]
    public void LoadFromDirectory_ReturnsFilesSortedByVersion()
    {
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);

        migrations.Should().HaveCount(3);
        migrations[0].Version.Should().Be(1);
        migrations[1].Version.Should().Be(2);
        migrations[2].Version.Should().Be(3);
    }

    [Fact]
    public void LoadFromDirectory_NonExistentDirectory_ReturnsEmpty()
    {
        var migrations = MigrationFile.LoadFromDirectory("/nonexistent/path");
        migrations.Should().BeEmpty();
    }

    [Fact]
    public void GetContent_ReturnsFileContents()
    {
        var filePath = Path.Combine(FixturesPath, "001_create_extensions.sql");
        var migration = MigrationFile.Parse(filePath)!;

        var content = migration.GetContent();
        content.Should().Contain("CREATE EXTENSION");
    }

    [Fact]
    public void GetChecksum_ReturnsSHA256Hex()
    {
        var filePath = Path.Combine(FixturesPath, "001_create_extensions.sql");
        var migration = MigrationFile.Parse(filePath)!;

        var checksum = migration.GetChecksum();
        checksum.Should().HaveLength(64); // SHA256 = 64 hex chars
        checksum.Should().MatchRegex("^[a-f0-9]{64}$");
    }

    [Fact]
    public void GetChecksum_SameFile_ReturnsSameHash()
    {
        var filePath = Path.Combine(FixturesPath, "001_create_extensions.sql");
        var m1 = MigrationFile.Parse(filePath)!;
        var m2 = MigrationFile.Parse(filePath)!;

        m1.GetChecksum().Should().Be(m2.GetChecksum());
    }

    [Fact]
    public void CompareTo_SortsByVersion()
    {
        var migrations = MigrationFile.LoadFromDirectory(FixturesPath);
        var sorted = migrations.OrderBy(m => m).ToList();

        sorted[0].Version.Should().Be(1);
        sorted[1].Version.Should().Be(2);
        sorted[2].Version.Should().Be(3);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var filePath = Path.Combine(FixturesPath, "002_create_users_table.sql");
        var migration = MigrationFile.Parse(filePath)!;

        migration.ToString().Should().Be("[002] Create users table");
    }
}
