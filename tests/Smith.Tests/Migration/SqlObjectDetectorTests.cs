using FluentAssertions;
using Smith.Database;
using Smith.Migration;

namespace Smith.Tests.Migration;

public class SqlObjectDetectorTests
{
    [Fact]
    public void ExtractObjects_CreateTable_DetectsTable()
    {
        var sql = "CREATE TABLE users (id SERIAL PRIMARY KEY);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Table, "users"));
    }

    [Fact]
    public void ExtractObjects_CreateTableIfNotExists_DetectsTable()
    {
        var sql = "CREATE TABLE IF NOT EXISTS users (id SERIAL PRIMARY KEY);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Table, "users"));
    }

    [Fact]
    public void ExtractObjects_CreateTableWithSchema_DetectsSchemaAndTable()
    {
        var sql = "CREATE TABLE myschema.users (id SERIAL PRIMARY KEY);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Table, "users", "myschema"));
    }

    [Fact]
    public void ExtractObjects_CreateFunction_DetectsFunction()
    {
        var sql = """
            CREATE OR REPLACE FUNCTION update_timestamp()
            RETURNS TRIGGER AS $$ BEGIN NEW.updated_at = NOW(); RETURN NEW; END; $$ LANGUAGE plpgsql;
            """;
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Function, "update_timestamp"));
    }

    [Fact]
    public void ExtractObjects_CreateExtension_DetectsExtension()
    {
        var sql = """
            CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
            CREATE EXTENSION IF NOT EXISTS "pgcrypto";
            """;
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().HaveCount(2);
        objects.Should().Contain(new DatabaseObject(DatabaseObjectType.Extension, "uuid-ossp"));
        objects.Should().Contain(new DatabaseObject(DatabaseObjectType.Extension, "pgcrypto"));
    }

    [Fact]
    public void ExtractObjects_CreateIndex_DetectsIndex()
    {
        var sql = "CREATE INDEX idx_users_email ON users (email);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Index, "idx_users_email"));
    }

    [Fact]
    public void ExtractObjects_CreateUniqueIndex_DetectsIndex()
    {
        var sql = "CREATE UNIQUE INDEX idx_users_username ON users (username);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Index, "idx_users_username"));
    }

    [Fact]
    public void ExtractObjects_CreateIndexConcurrently_DetectsIndex()
    {
        var sql = "CREATE INDEX CONCURRENTLY idx_articles_title ON articles (title);";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Index, "idx_articles_title"));
    }

    [Fact]
    public void ExtractObjects_CreateTrigger_DetectsTrigger()
    {
        var sql = "CREATE TRIGGER update_users_timestamp BEFORE UPDATE ON users FOR EACH ROW EXECUTE FUNCTION update_timestamp();";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Trigger, "update_users_timestamp"));
    }

    [Fact]
    public void ExtractObjects_CreateView_DetectsView()
    {
        var sql = "CREATE VIEW active_users AS SELECT * FROM users WHERE active = true;";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.View, "active_users"));
    }

    [Fact]
    public void ExtractObjects_CreateMaterializedView_DetectsView()
    {
        var sql = "CREATE MATERIALIZED VIEW user_stats AS SELECT COUNT(*) FROM users;";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.View, "user_stats"));
    }

    [Fact]
    public void ExtractObjects_ComplexMigration_DetectsAllObjects()
    {
        var sql = """
            CREATE TABLE users (id SERIAL PRIMARY KEY, name TEXT);
            CREATE TABLE articles (id SERIAL PRIMARY KEY, title TEXT);
            CREATE INDEX idx_articles_title ON articles (title);
            CREATE OR REPLACE FUNCTION update_timestamp() RETURNS TRIGGER AS $$ BEGIN RETURN NEW; END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER update_users_ts BEFORE UPDATE ON users EXECUTE FUNCTION update_timestamp();
            """;
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().HaveCount(5);
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Table && o.Name == "users");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Table && o.Name == "articles");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Index && o.Name == "idx_articles_title");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Function && o.Name == "update_timestamp");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Trigger && o.Name == "update_users_ts");
    }

    [Fact]
    public void ExtractObjects_IgnoresComments()
    {
        var sql = """
            -- CREATE TABLE should_not_match (id INT);
            /* CREATE TABLE also_not_matched (id INT); */
            CREATE TABLE actual_table (id SERIAL PRIMARY KEY);
            """;
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle()
            .Which.Should().Be(new DatabaseObject(DatabaseObjectType.Table, "actual_table"));
    }

    [Fact]
    public void ExtractObjects_Deduplicates()
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS users (id SERIAL PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS users (id SERIAL PRIMARY KEY);
            """;
        var objects = SqlObjectDetector.ExtractObjects(sql);

        objects.Should().ContainSingle();
    }

    [Fact]
    public void ExtractObjects_EmptySQL_ReturnsEmpty()
    {
        SqlObjectDetector.ExtractObjects("").Should().BeEmpty();
        SqlObjectDetector.ExtractObjects("-- just a comment").Should().BeEmpty();
        SqlObjectDetector.ExtractObjects("SELECT 1;").Should().BeEmpty();
    }

    [Fact]
    public void ExtractObjects_AlterTable_ReturnsEmpty()
    {
        var sql = "ALTER TABLE users ADD COLUMN email_verified BOOLEAN;";
        var objects = SqlObjectDetector.ExtractObjects(sql);

        // Reason: ALTER TABLE 不是 CREATE，不产生新对象
        objects.Should().BeEmpty();
    }

    [Fact]
    public void ExtractObjects_FixtureFile_DetectsCorrectObjects()
    {
        var fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var migration = MigrationFile.Parse(Path.Combine(fixturesPath, "002_create_users_table.sql"))!;
        var objects = SqlObjectDetector.ExtractObjects(migration.GetContent());

        objects.Should().HaveCount(3);
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Table && o.Name == "users");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Index && o.Name == "idx_users_username");
        objects.Should().Contain(o => o.Type == DatabaseObjectType.Index && o.Name == "idx_users_email");
    }
}
