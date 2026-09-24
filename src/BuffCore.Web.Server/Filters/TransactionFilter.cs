using BuffCore.Abstractions.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Net;

namespace BuffCore.Web.Server.Filters
{
    /// <summary>
    /// Middleware that wraps an API action within a database transaction scope.
    /// It commits the transaction on successful mutation HTTP methods; otherwise, rolls it back.
    /// </summary>
    /// <typeparam name="TApplicationDbContext">The application's DbContext type.</typeparam>
    public class TransactionFilter<TApplicationDbContext>(TApplicationDbContext _dbContext, ILogger<TransactionFilter<TApplicationDbContext>> _logger) : IAsyncActionFilter
        where TApplicationDbContext : DbContext
    {
        private static readonly string[] MutationMethods = [HttpMethods.Post, HttpMethods.Patch, HttpMethods.Put, HttpMethods.Delete];

        /// <summary>
        /// Executes the action within a transaction, committing or rolling back based on result status code.
        /// </summary>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var cancellationToken = context.HttpContext.RequestAborted;
            var requestMethod = context.HttpContext.Request.Method;
            var isMutationRequest = MutationMethods.Contains(requestMethod, StringComparer.OrdinalIgnoreCase);

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                _logger.LogInformation("Database transaction started for action: {Action}", context.ActionDescriptor.DisplayName);

                var resultContext = await next();
                cancellationToken.ThrowIfCancellationRequested();

                if (resultContext.Exception == null)
                {
                    switch (resultContext.Result)
                    {
                        case ObjectResult objectResult:
                            await HandleObjectResult(context, transaction, objectResult, isMutationRequest, cancellationToken);
                            break;

                        case FileStreamResult fileStreamResult:
                            await transaction.RollbackAsync(CancellationToken.None);
                            _logger.LogInformation("Transaction rolled back for FileStreamResult (read-only operation)");
                            break;

                        default:
                            await transaction.RollbackAsync(CancellationToken.None);
                            _logger.LogWarning("Transaction rolled back: Unrecognized result type from action.");
                            break;
                    }
                }
                else
                {
                    await RollbackTransaction(transaction, "Exception thrown during action execution.");
                    HandleUnexpectedException(context, resultContext.Exception, "An exception occurred during action execution.");

                    resultContext.Result = context.Result;
                    resultContext.Exception = null;
                }
            }
            catch (OperationCanceledException)
            {
                if (_dbContext.Database.CurrentTransaction != null)
                {
                    await _dbContext.Database.CurrentTransaction
                        .RollbackAsync(CancellationToken.None);
                }

                _logger.LogInformation("Request was cancelled by client.");

                return;
            }
            catch (Exception ex)
            {
                if (_dbContext.Database.CurrentTransaction != null)
                    await RollbackTransaction(_dbContext.Database.CurrentTransaction, "Unhandled exception in transaction filter.");

                HandleUnexpectedException(context, ex, "Unhandled error during transaction handling.");
            }
        }

        /// <summary>
        /// Handles the logic for an ObjectResult, including validation of accepted response codes and transaction finalization.
        /// </summary>
        private async Task HandleObjectResult(ActionExecutingContext context, IDbContextTransaction transaction, ObjectResult objectResult, bool isMutationRequest, CancellationToken cancellationToken)
        {
            if (objectResult.Value is not BaseDto baseDto)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogWarning("Transaction rolled back: Result does not implement BaseDto.");
                return;
            }

            // Ensure response carries proper HTTP status code and trace ID
            context.HttpContext.Response.StatusCode = baseDto.Code;
            baseDto.Id = context.HttpContext.TraceIdentifier;

            if (!isMutationRequest)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogInformation("Transaction rolled back. Endpoint is not a mutation HTTP method: {Method}.", context.HttpContext.Request.Method);
                return;
            }

            if (baseDto.Code >= 200 && baseDto.Code < 300 || baseDto.CommitTransaction)
            {
                await transaction.CommitAsync(CancellationToken.None);
                _logger.LogInformation("Transaction committed. Status code accepted: {Code}, commit flag: {Commit}", baseDto.Code, baseDto.CommitTransaction);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogWarning("Transaction rolled back. Unsuccessful mutation status code: {Code}, commit flag: {Commit}", baseDto.Code, baseDto.CommitTransaction);
            }
        }

        /// <summary>
        /// Rolls back the current transaction with a reason for logging.
        /// </summary>
        private async Task RollbackTransaction(IDbContextTransaction transaction, string reason)
        {
            if (_dbContext.Database.CurrentTransaction != null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError("Transaction rolled back. Reason: {Reason}", reason);
            }
        }

        /// <summary>
        /// Constructs and logs an error response for unexpected exceptions.
        /// </summary>
        private void HandleUnexpectedException(ActionExecutingContext context, Exception ex, string errorMessage)
        {
            var errorResponse = new BaseDto($"An error occurred. Trace ID: {context.HttpContext.TraceIdentifier}", HttpStatusCode.InternalServerError)
            {
                Id = context.HttpContext.TraceIdentifier
            };

            context.Result = new JsonResult(errorResponse);
            context.HttpContext.Response.StatusCode = errorResponse.Code;

            _logger.LogError(ex, "{Message}", errorMessage);
        }
    }
}
