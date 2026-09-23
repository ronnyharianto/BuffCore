using BuffCore.Data;
using BuffCore.Data.Configurations;
using BuffCore.Data.Enums;
using BuffCore.Data.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BuffCore.Data.Tests
{
    public class RegistrationAndMigrationTests
    {
        [Fact]
        public void AddBuffCoreData_RegistersContextAndAppliesConventions()
        {
            using var host = TestHostFactory.Build();

            var dbContextOptions = host.Services.GetRequiredService<DbContextOptions<EmptyDbContext>>();
            var relational = dbContextOptions.Extensions.OfType<RelationalOptionsExtension>().Single();

            Assert.Equal(33, relational.CommandTimeout);
            Assert.Equal("BuffCore.Data.Tests", relational.MigrationsAssembly);
        }

        [Fact]
        public void AddBuffCoreData_WithoutProviderDelegate_ThrowsMeaningfulError()
        {
            var services = new ServiceCollection();

            Assert.Throws<InvalidOperationException>(
                () => services.AddBuffCoreData<EmptyDbContext>(new DatabaseConfig(), x => { }));
        }

        [Fact]
        public async Task MigrateDatabaseAsync_AppliesPendingMigrations()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            // The migration's SQL creates a table inside the 'custom' schema, which SQLite models
            // as an attached database; attach one so the schema-qualified DDL can execute.
            using (var attach = connection.CreateCommand())
            {
                attach.CommandText = "ATTACH ':memory:' AS custom";
                attach.ExecuteNonQuery();
            }

            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddBuffCoreData<WidgetDbContext>(
                        new DatabaseConfig(),
                        x => x.UseSqlite(
                            connection,
                            o => o.MigrationsAssembly(typeof(Widgets001).Assembly.FullName)));
                })
                .Build();

            await host.MigrateDatabaseAsync<WidgetDbContext>();

            // The tables exist because the migration's SQL actually ran.
            Assert.Equal(1, connection.ExecuteScalarCount("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TaggedWidgets'"));

            // The history row was written by the migrator itself.
            var db = host.Services.GetRequiredService<WidgetDbContext>();
            var applied = await db.Database.GetAppliedMigrationsAsync();

            Assert.Contains(Widgets001.MigrationId, applied);
        }

        [Fact]
        public void DbCollationHelper_ResolvesCaseInsensitiveCollations()
        {
            Assert.Equal("SQL_Latin1_General_CP1_CI_AS", DbCollationHelper.GetCaseInsensitiveCollation(DatabaseEngine.SQLServer));
            Assert.Equal("en_US.UTF-8", DbCollationHelper.GetCaseInsensitiveCollation(DatabaseEngine.PostgreSQL));
        }

        [Fact]
        public void DbCollationHelper_ResolvesCaseSensitiveCollations()
        {
            Assert.Equal("SQL_Latin1_General_CP1_CS_AS", DbCollationHelper.GetCaseSensitiveCollation(DatabaseEngine.SQLServer));
            Assert.Equal("C", DbCollationHelper.GetCaseSensitiveCollation(DatabaseEngine.PostgreSQL));
        }

        [Fact]
        public void DbCollationHelper_ThrowsForUnsupportedEngine()
        {
            Assert.Throws<NotSupportedException>(() => DbCollationHelper.GetCaseInsensitiveCollation((DatabaseEngine)999));
        }
    }

    internal static class SqliteConnectionExtensions
    {
        /// <summary>Runs a scalar count query over the open connection via plain ADO.NET.</summary>
        public static long ExecuteScalarCount(this SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            return Convert.ToInt64(command.ExecuteScalar());
        }
    }
}
