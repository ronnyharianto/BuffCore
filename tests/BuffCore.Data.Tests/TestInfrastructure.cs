using BuffCore.Abstractions;
using BuffCore.Data;
using BuffCore.Data.Attributes;
using BuffCore.Data.Configurations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BuffCore.Data.Tests
{
    /// <summary>Entity placed in a custom schema via <see cref="DatabaseSchemaAttribute"/>.</summary>
    [DatabaseSchema("custom")]
    public class TaggedWidget : EntityBase
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Entity left in the default schema (no attribute).</summary>
    public class PlainWidget : EntityBase
    {
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Context exercising schema mapping with SQLite (schema == attached database name).</summary>
    public class WidgetDbContext(DbContextOptions options, CurrentUserAccessor? currentUser = null)
        : AuditDbContext(options, currentUser)
    {
        public DbSet<TaggedWidget> TaggedWidgets { get; set; }
        public DbSet<PlainWidget> PlainWidgets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            new EntityBaseBuilder<TaggedWidget>().Configure(modelBuilder.Entity<TaggedWidget>());
            new EntityBaseBuilder<PlainWidget>().Configure(modelBuilder.Entity<PlainWidget>());
        }
    }

    /// <summary>Context with no DbSets, used for registration tests.</summary>
    public class EmptyDbContext(DbContextOptions options) : AuditDbContext(options)
    {
    }

    /// <summary>
    /// Hand-written migration discovered by the migrator exactly like scaffolded migrations are:
    /// via <see cref="DbContextAttribute"/> and <see cref="MigrationAttribute"/> on the test assembly
    /// (registered as the migrations assembly in RegistrationAndMigrationTests). Emits real
    /// <see cref="SqlOperation"/>s creating the backing tables — including inside the attached
    /// 'custom' database, emulating a schema — so <see cref="HostExtensions.MigrateDatabaseAsync{TDbContext}"/>
    /// executes actual migration SQL.
    /// </summary>
    [DbContext(typeof(WidgetDbContext))]
    [Migration(Widgets001.MigrationId)]
    public class Widgets001 : Migration
    {
        public const string MigrationId = "20260923000000_Widgets001";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Operations.Add(new SqlOperation
            {
                Sql =
                    """
                    CREATE TABLE IF NOT EXISTS "TaggedWidgets" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_TaggedWidgets" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Created" TEXT NOT NULL,
                        "CreatedBy" TEXT NOT NULL,
                        "Modified" TEXT NULL,
                        "ModifiedBy" TEXT NULL,
                        "RowStatus" INTEGER NOT NULL DEFAULT 0
                    );
                    CREATE TABLE IF NOT EXISTS custom."TaggedWidgets" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_custom_TaggedWidgets" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Created" TEXT NOT NULL,
                        "CreatedBy" TEXT NOT NULL,
                        "Modified" TEXT NULL,
                        "ModifiedBy" TEXT NULL,
                        "RowStatus" INTEGER NOT NULL DEFAULT 0
                    );
                    """
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Operations.Add(new SqlOperation { Sql = "DROP TABLE IF EXISTS custom.\"TaggedWidgets\";" });
            migrationBuilder.Operations.Add(new SqlOperation { Sql = "DROP TABLE IF EXISTS \"TaggedWidgets\";" });
        }
    }

    public static class TestHostFactory
    {
        /// <summary>
        /// Builds a real <see cref="Host"/> whose DI container registers <see cref="EmptyDbContext"/>
        /// via <see cref="ServiceCollectionExtensions.AddBuffCoreData{TDbContext}"/> with SQLite.
        /// </summary>
        public static IHost Build(Action<IServiceCollection>? configureServices = null)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            return Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddBuffCoreData<EmptyDbContext>(
                        new DatabaseConfig { ConnectionString = "DataSource=:memory:", CommandTimeout = 33 },
                        x => x.UseSqlite(connection));

                    configureServices?.Invoke(services);
                })
                .Build();
        }
    }
}
