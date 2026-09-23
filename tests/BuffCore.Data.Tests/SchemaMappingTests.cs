using BuffCore.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BuffCore.Data.Tests
{
    public class SchemaMappingTests
    {
        private static (WidgetDbContext Context, SqliteConnection Connection) CreateContext()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<WidgetDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new WidgetDbContext(options);
            context.Database.EnsureCreated();

            return (context, connection);
        }

        [Fact]
        public void EntitiesWithAttribute_AreMappedToDeclaredSchema()
        {
            var (context, connection) = CreateContext();
            using var _ = connection;

            var entityType = context.Model.FindEntityType(typeof(TaggedWidget));

            Assert.NotNull(entityType);
            Assert.Equal("custom", entityType.GetSchema());
            Assert.Equal("TaggedWidgets", entityType.GetTableName());
        }

        [Fact]
        public void EntitiesWithoutAttribute_AreMappedToPublicSchema()
        {
            var (context, connection) = CreateContext();
            using var _ = connection;

            var entityType = context.Model.FindEntityType(typeof(PlainWidget));

            Assert.NotNull(entityType);
            Assert.Equal("public", entityType.GetSchema());
            Assert.Equal("PlainWidgets", entityType.GetTableName());
        }

        [Fact]
        public void OperationsAgainstTaggedEntity_WorkThroughTheMappedTable()
        {
            var (context, connection) = CreateContext();
            using var _ = connection;

            context.TaggedWidgets.Add(new TaggedWidget { Name = "tagged" });
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var stored = context.TaggedWidgets.Single();

            Assert.Equal("tagged", stored.Name);

            // SQLite has no cross-database DML (its "schemas" are attached databases), so physical
            // placement cannot be proven here — the schema *declaration* is asserted via GetSchema()
            // in the tests above. The mapped table name and roundtrip are still verified for real.
            var countInMappedTable = connection.ExecuteScalarCount("SELECT COUNT(*) FROM \"TaggedWidgets\"");

            Assert.Equal(1, countInMappedTable);
        }
    }
}
