using BuffCore.Abstractions;
using BuffCore.Abstractions.Dtos;
using BuffCore.Utilities;
using BuffCore.Web.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;

namespace BuffCore.Web.Server
{
    /// <summary>
    /// Locates an API client by its public <c>X-CLIENT-ID</c> identifier. Implemented by the host
    /// against its own client storage; the returned principal is validated generically by
    /// <see cref="PermissionAuthorizationFilter"/> (enabled, expiry, key hash).
    /// </summary>
    /// <param name="clientId">The public client identifier from the <c>X-CLIENT-ID</c> header.</param>
    /// <returns>The stored client, or null when unknown.</returns>
    public delegate ApiPrincipal? FindApiClientDelegate(string clientId);

    /// <summary>
    /// Invoked after an API client has been fully authenticated, allowing hosts to record
    /// e.g. a <c>LastUsedAt</c> timestamp. Failures to persist are the host's concern.
    /// </summary>
    /// <param name="principal">The authenticated client principal.</param>
    public delegate void ClientAuthenticatedDelegate(ApiPrincipal principal);

    /// <summary>
    /// Authorization filter that enforces permission checks based on bearer token claims.
    /// Reads required permissions from <see cref="PermissionAuthorizeAttribute"/> and compares them
    /// to the user's token claims, and authenticates API-key callers via the host-supplied
    /// <see cref="FindApiClientDelegate"/>. Populates <see cref="CurrentUserAccessor"/> on success.
    /// </summary>
    public class PermissionAuthorizationFilter(
        ILogger<PermissionAuthorizationFilter> _logger,
        CurrentUserAccessor _currentUserAccessor,
        FindApiClientDelegate? _findApiClient,
        CurrentClaimsOptions? _claimsOptions = null,
        ClientAuthenticatedDelegate? _clientAuthenticated = null) : IAuthorizationFilter
    {
        /// <summary>
        /// Called by the framework to authorize an HTTP request.
        /// </summary>
        /// <param name="context">Authorization filter context containing HTTP context and endpoint metadata.</param>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            try
            {
                // Skip if endpoint allows anonymous access
                if (context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
                {
                    _logger.LogDebug("Skipping authorization (AllowAnonymousAttribute found).");

                    return;
                }

                var headerToken = context.HttpContext.Request.Headers.Authorization;
                _logger.LogDebug("Authorization header received: {Token}", string.IsNullOrWhiteSpace(headerToken) ? "[empty]" : "[token provided]");

                var appAuthorizeApiKeyAttribute = context.ActionDescriptor.EndpointMetadata.OfType<PermissionAuthorizeApiKeyAttribute>().FirstOrDefault();
                var isAllowApiKey = appAuthorizeApiKeyAttribute is not null;

                // ======================
                // API KEY AUTHENTICATION
                // ======================

                if (isAllowApiKey)
                {
                    if (AuthorizeApiClient(context))
                    {
                        return;
                    }
                }

                // ======================
                // JWT AUTHENTICATION
                // ======================

                if (string.IsNullOrWhiteSpace(headerToken))
                {
                    _logger.LogWarning("The access is denied, because no authorization information was provided.");
                }
                else if (context.HttpContext.User.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                {
                    _logger.LogWarning("The access is denied, because the aunthentication failed.");
                }
                else if (identity.Claims is { } claims && claims.Any())
                {
                    var permissionsClaimType = _claimsOptions?.PermissionsClaimType ?? BuffCoreClaimTypes.Permissions;
                    var permissions = identity.Claims.Where(i => i.Type == permissionsClaimType);

                    if (IsAuthorize(context, permissions, _logger))
                    {
                        // Extract identity info and populate CurrentUserAccessor.
                        var accessor = CurrentClaimsMapper.Map(context.HttpContext.User, _claimsOptions);
                        _currentUserAccessor.UserId = accessor.UserId;
                        _currentUserAccessor.FullName = accessor.FullName;
                        _currentUserAccessor.EmailAddress = accessor.EmailAddress;
                        _currentUserAccessor.CompanyId = accessor.CompanyId;
                        _currentUserAccessor.Permissions = accessor.Permissions;

                        _logger.LogDebug("Authorization succeeded. UserId={UserId}, CompanyId={CompanyId}", _currentUserAccessor.UserId, _currentUserAccessor.CompanyId);

                        return;
                    }

                    _logger.LogWarning("User does not have required permissions.");
                }

                // If not authorized, return error response
                var unAuthorizedResponse = new BaseDto("Authorization has been denied for this request.", HttpStatusCode.Unauthorized)
                {
                    Id = context.HttpContext.TraceIdentifier
                };

                context.HttpContext.Response.StatusCode = unAuthorizedResponse.Code;
                context.Result = new JsonResult(unAuthorizedResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during authorization.");

                var errorResponse = new BaseDto("An error occurred while authorizing the request.", HttpStatusCode.InternalServerError)
                {
                    Id = context.HttpContext.TraceIdentifier
                };

                context.HttpContext.Response.StatusCode = errorResponse.Code;
                context.Result = new JsonResult(errorResponse);
            }
        }

        /// <summary>
        /// Checks if the user has the required permission(s) to access the endpoint.
        /// </summary>
        /// <param name="context">The current authorization context.</param>
        /// <param name="claims">The user's permission claims.</param>
        /// <param name="logger">Logger instance.</param>
        /// <returns><c>true</c> if authorized; otherwise, <c>false</c>.</returns>
        private static bool IsAuthorize(AuthorizationFilterContext context, IEnumerable<Claim> claims, ILogger<PermissionAuthorizationFilter> logger)
        {
            var permissionAuthorizeAttribute = context.ActionDescriptor.EndpointMetadata.OfType<PermissionAuthorizeAttribute>().FirstOrDefault();
            if (permissionAuthorizeAttribute is null)
            {
                logger.LogWarning("Authorization failed: No PermissionAuthorizeAttribute found.");
                return false;
            }
            else if (permissionAuthorizeAttribute.Permissions.Length is 0)
            {
                logger.LogInformation("Authorization successful: Allow for authenticated users.");

                return true;
            }

            var matched = claims.Where(c => permissionAuthorizeAttribute.Permissions.Contains(c.Value, StringComparer.OrdinalIgnoreCase)).Select(c => c.Value);
            if (matched.Any())
            {
                logger.LogInformation("Authorization successful: Matches permission(s): {Permissions}", string.Join(", ", matched));
                return true;
            }

            logger.LogWarning("Authorization failed: No matching permissions found.");
            return false;
        }

        /// <summary>
        /// Checks if the API client is authorized to access the endpoint.
        /// </summary>
        /// <param name="context">The current authorization context.</param>
        /// <returns><c>true</c> if authorized; otherwise, <c>false</c>.</returns>
        private bool AuthorizeApiClient(AuthorizationFilterContext context)
        {
            if (_findApiClient is null)
            {
                _logger.LogWarning("API client authorization failed because the host did not configure an API client lookup.");

                return false;
            }

            var clientId = context.HttpContext.Request.Headers["X-CLIENT-ID"].FirstOrDefault();
            var apiKey = context.HttpContext.Request.Headers["X-API-KEY"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("API client authorization failed because headers are missing.");

                return false;
            }

            var apiClient = _findApiClient(clientId);
            if (apiClient is null)
            {
                _logger.LogWarning("API client authorization failed because client was not found. ClientId={ClientId}", clientId);

                return false;
            }

            if (!apiClient.Enabled)
            {
                _logger.LogWarning("API client authorization failed because client is disabled. ClientId={ClientId}", clientId);

                return false;
            }

            if (apiClient.ExpiresAt.HasValue && apiClient.ExpiresAt.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("API client authorization failed because api key expired. ClientId={ClientId}", clientId);

                return false;
            }

            var hashedApiKey = HashHelper.ComputeSha256(apiKey);

            if (!string.Equals(apiClient.ApiKeyHash, hashedApiKey, StringComparison.Ordinal))
            {
                _logger.LogWarning("API client authorization failed because api key invalid. ClientId={ClientId}", clientId);

                return false;
            }

            _clientAuthenticated?.Invoke(apiClient);

            // populate accessor
            _currentUserAccessor.UserId = apiClient.Id;
            _currentUserAccessor.FullName = apiClient.Name;
            _currentUserAccessor.EmailAddress = $"{apiClient.ClientId}@integration";
            _currentUserAccessor.Permissions = [];

            _logger.LogInformation("API client authorization succeeded");

            return true;
        }
    }
}
