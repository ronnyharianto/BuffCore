using BuffCore.Abstractions.Dtos;
using BuffCore.Web.Server.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace BuffCore.Web.Server.Tests
{
    /// <summary>
    /// Covers the commit/rollback decision matrix of <see cref="TransactionFilter{WidgetDbContext}"/> —
    /// exercised here through the concrete <see cref="WidgetDbContext"/>.
    /// </summary>
    public class TransactionFilterTests
    {
        private static TransactionFilter<WidgetDbContext> CreateFilter(WidgetDbContext dbContext)
            => new(dbContext, NullLogger<TransactionFilter<WidgetDbContext>>.Instance);

        private async Task<(ActionExecutingContext Context, WidgetDbContext DbContext)> RunAsync(
            string httpMethod,
            IActionResult? actionResult = null,
            Exception? actionException = null)
        {
            var scope = FilterTestHarness.CreateContext(httpMethod, actionResult, actionException);
            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, scope.Next);
            return (scope.Executing, scope.DbContext);
        }

        [Fact]
        public async Task Post_WithSuccessBaseDto_CommitsTransaction()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Post, new ObjectResult(new BaseDto("created", HttpStatusCode.OK)));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Equal((int)HttpStatusCode.OK, context.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Post_WithSuccessBaseDto_SetsResultOnExecutedContext()
        {
            var scope = FilterTestHarness.CreateContext(HttpMethods.Post, new ObjectResult(new BaseDto("created", HttpStatusCode.OK)));

            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, scope.Next);

            // The success path doesn't touch the executing context's Result; the action's result
            // remains visible on the executed context, exactly as the MVC pipeline expects.
            Assert.Null(scope.Executing.Result);
            Assert.NotNull(scope.Executed);
            Assert.IsType<ObjectResult>(scope.Executed!.Result);
        }

        [Fact]
        public async Task Post_WithSuccessBaseDto_StampsDtoId()
        {
            var scope = FilterTestHarness.CreateContext(HttpMethods.Post, new ObjectResult(new BaseDto("created", HttpStatusCode.OK)));

            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, scope.Next);

            var baseDto = Assert.IsType<BaseDto>(Assert.IsType<ObjectResult>(scope.Executed!.Result).Value);
            Assert.Equal("trace-test-001", baseDto.Id);
        }

        [Fact]
        public async Task Post_WithFailureBaseDto_RollsBackTransaction()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Post, new ObjectResult(new BaseDto("denied", HttpStatusCode.BadRequest)));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Equal((int)HttpStatusCode.BadRequest, context.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Post_WithCommitTransactionFlag_CommitsDespiteFailureCode()
        {
            var baseDto = new BaseDto("denied but committed", HttpStatusCode.BadRequest) { CommitTransaction = true };

            var (context, dbContext) = await RunAsync(HttpMethods.Post, new ObjectResult(baseDto));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Equal((int)HttpStatusCode.BadRequest, context.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Get_WithSuccessBaseDto_RollsBackBecauseNotMutation()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Get, new ObjectResult(new BaseDto("ok", HttpStatusCode.OK)));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Equal((int)HttpStatusCode.OK, context.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Get_WithFileStreamResult_RollsBackAndLeavesResultUntouched()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Get, new FileStreamResult(Stream.Null, "application/octet-stream"));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task Post_WithNonBaseDtoValue_RollsBack()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Post, new ObjectResult(new { arbitrary = 42 }));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task Post_WithActionException_Produces500JsonResult()
        {
            var (context, dbContext) = await RunAsync(HttpMethods.Post, actionException: new InvalidOperationException("boom"));

            Assert.Null(dbContext.Database.CurrentTransaction);
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.HttpContext.Response.StatusCode);

            // The filter replaces the result with a JsonResult carrying the BaseDto error envelope.
            var value = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Contains("trace-test-001", value.Message);
        }

        [Fact]
        public async Task CancelledRequest_RollsBackAndSwallows()
        {
            var scope = FilterTestHarness.CreateContext(HttpMethods.Post);

            // Simulate the client disconnecting mid-action: the next delegate throws OCE.
            ActionExecutionDelegate throwingNext = () => Task.FromException<ActionExecutedContext>(
                new OperationCanceledException(scope.Executing.HttpContext.RequestAborted));

            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, throwingNext);

            Assert.Null(scope.DbContext.Database.CurrentTransaction);
            Assert.Null(scope.Executing.Result);
        }

        [Fact]
        public async Task Mutations_CanWriteWithinCommittedTransaction()
        {
            var scope = FilterTestHarness.CreateContext(HttpMethods.Post);

            ActionExecutionDelegate writingNext = () =>
            {
                scope.DbContext.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "persisted" });
                scope.DbContext.SaveChanges();

                var executed = new ActionExecutedContext(scope.Executing, [], scope.Executing.Controller)
                {
                    Result = new ObjectResult(new BaseDto("created", HttpStatusCode.OK)),
                };
                scope.Executed = executed;
                return Task.FromResult(executed);
            };

            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, writingNext);

            Assert.Null(scope.DbContext.Database.CurrentTransaction);
            Assert.Equal(1, await scope.DbContext.Widgets.CountAsync());
        }

        [Fact]
        public async Task Mutations_WithFailureResult_DoNotPersistWrites()
        {
            var scope = FilterTestHarness.CreateContext(HttpMethods.Post);

            ActionExecutionDelegate writingNext = () =>
            {
                scope.DbContext.Widgets.Add(new Widget { Id = Guid.NewGuid(), Name = "discarded" });
                scope.DbContext.SaveChanges();

                var executed = new ActionExecutedContext(scope.Executing, [], scope.Executing.Controller)
                {
                    Result = new ObjectResult(new BaseDto("rejected", HttpStatusCode.UnprocessableEntity)),
                };
                scope.Executed = executed;
                return Task.FromResult(executed);
            };

            await CreateFilter(scope.DbContext).OnActionExecutionAsync(scope.Executing, writingNext);

            Assert.Null(scope.DbContext.Database.CurrentTransaction);
            Assert.Equal(0, await scope.DbContext.Widgets.CountAsync());
        }
    }
}
