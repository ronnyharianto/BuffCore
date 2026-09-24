using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuffCore.Web.Server.Tests
{
    /// <summary>Simple entity used by the SQLite-backed test context.</summary>
    public class Widget
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>SQLite in-memory context exercising <see cref="BuffCore.Web.Server.Filters.TransactionFilter{TApplicationDbContext}"/>.</summary>
    public class WidgetDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Widget> Widgets => Set<Widget>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Widget>().HasKey(w => w.Id);
        }
    }

    /// <summary>
    /// One filter-execution scenario: the executing context handed to the filter, the delegate
    /// emulating the MVC pipeline (which either returns a result or throws), the executed context
    /// captured when the delegate runs, and the SQLite-backed context.
    /// </summary>
    public sealed class FilterTestScope
    {
        /// <summary>Context the filter receives before the action runs.</summary>
        public required ActionExecutingContext Executing { get; init; }

        /// <summary>Delegate the filter invokes to run the action.</summary>
        public required ActionExecutionDelegate Next { get; set; }

        /// <summary>Context captured when <see cref="Next"/> runs; null if the action threw.</summary>
        public ActionExecutedContext? Executed { get; set; }

        /// <summary>Backing context for the filter.</summary>
        public required WidgetDbContext DbContext { get; init; }
    }

    /// <summary>
    /// Builds filter-execution contexts against a real <see cref="DefaultHttpContext"/> so status-code
    /// stamping and trace-identifier behavior are exercised exactly like production.
    /// </summary>
    public static class FilterTestHarness
    {
        public static FilterTestScope CreateContext(
            string httpMethod,
            IActionResult? actionResult = null,
            Exception? actionException = null)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<WidgetDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new WidgetDbContext(options);
            dbContext.Database.EnsureCreated();

            var services = new ServiceCollection();
            services.AddSingleton(dbContext);
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
            services.AddLogging();
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
                TraceIdentifier = "trace-test-001"
            };
            httpContext.Request.Method = httpMethod;

            var actionDescriptor = new ActionDescriptor();
            var filters = new List<IFilterMetadata>();

            var actionExecutingContext = new ActionExecutingContext(
                new ActionContext(httpContext, new RouteData(), actionDescriptor),
                filters,
                new Dictionary<string, object?>(),
                new object());

            var scope = new FilterTestScope
            {
                Executing = actionExecutingContext,
                DbContext = dbContext,
                Next = null!,
            };

            // Emulates the MVC pipeline: the action either returns a result or throws; the executed
            // context is captured so tests can assert on the result the filter observed.
            scope.Next = () =>
            {
                var executed = new ActionExecutedContext(actionExecutingContext, filters, actionExecutingContext.Controller)
                {
                    Result = actionResult,
                    Exception = actionException,
                };
                scope.Executed = executed;
                return Task.FromResult(executed);
            };

            return scope;
        }
    }
}
