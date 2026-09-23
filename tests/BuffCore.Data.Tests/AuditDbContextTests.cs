using BuffCore.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BuffCore.Data.Tests
{
    public class AuditDbContextTests
    {
        private static (WidgetDbContext Context, SqliteConnection Connection) CreateContext(CurrentUserAccessor? accessor = null)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<WidgetDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new WidgetDbContext(options, accessor);
            context.Database.EnsureCreated();

            return (context, connection);
        }

        [Fact]
        public void SaveChanges_WithAccessor_StampsCreatedFieldsForAddedEntities()
        {
            var (context, connection) = CreateContext(new CurrentUserAccessor { UserId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e") });
            using var _ = connection;

            var widget = new PlainWidget { Name = "new" };
            context.PlainWidgets.Add(widget);
            context.SaveChanges();

            Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", widget.CreatedBy);
            Assert.NotEqual(default, widget.Created);
            Assert.Null(widget.Modified);
            Assert.Null(widget.ModifiedBy);
        }

        [Fact]
        public async Task SaveChangesAsync_WithoutAccessor_UsesEmptyActor()
        {
            var (context, connection) = CreateContext(null);
            using var _ = connection;

            var widget = new PlainWidget { Name = "anon" };
            context.PlainWidgets.Add(widget);
            await context.SaveChangesAsync();

            Assert.Equal(string.Empty, widget.CreatedBy);
            Assert.NotEqual(default, widget.Created);
        }

        [Fact]
        public void SaveChanges_StampsModifiedFieldsForUpdatedEntities()
        {
            var (context, connection) = CreateContext(new CurrentUserAccessor { UserId = Guid.Parse("de305d54-75b4-431b-adb2-eb6b9e546014") });
            using var _ = connection;

            var widget = new PlainWidget { Name = "before" };
            context.PlainWidgets.Add(widget);
            context.SaveChanges();

            widget.Name = "after";
            context.SaveChanges();

            Assert.Equal("de305d54-75b4-431b-adb2-eb6b9e546014", widget.ModifiedBy);
            Assert.NotNull(widget.Modified);
        }

        [Fact]
        public async Task SoftDeletedRows_AreExcludedByQueryFilter()
        {
            var (context, connection) = CreateContext(null);
            using var _ = connection;

            context.PlainWidgets.Add(new PlainWidget { Name = "active", RowStatus = 0 });
            context.PlainWidgets.Add(new PlainWidget { Name = "deleted", RowStatus = 1 });
            await context.SaveChangesAsync();

            var names = await context.PlainWidgets.Select(w => w.Name).ToListAsync();

            Assert.Contains("active", names);
            Assert.DoesNotContain("deleted", names);
        }
    }
}
