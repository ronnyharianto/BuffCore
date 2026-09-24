using BuffCore.Abstractions;
using BuffCore.Abstractions.Dtos;
using BuffCore.Utilities;
using BuffCore.Web.Server.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace BuffCore.Web.Server.Tests
{
    /// <summary>
    /// Covers <see cref="PermissionAuthorizationFilter"/>: anonymous skip, JWT permission matching,
    /// API-key authentication through the host delegate seam, and the 401/500 envelopes.
    /// </summary>
    public class PermissionAuthorizationFilterTests
    {
        private static AuthorizationFilterContext BuildContext(
            List<object> endpointMetadata,
            ClaimsPrincipal? principal = null,
            string? authorizationHeader = null,
            string? clientId = null,
            string? apiKey = null)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
                TraceIdentifier = "trace-auth-001",
            };

            if (principal is not null)
            {
                httpContext.User = principal;
            }
            if (!string.IsNullOrEmpty(authorizationHeader))
            {
                httpContext.Request.Headers.Authorization = authorizationHeader;
            }
            if (clientId is not null)
            {
                httpContext.Request.Headers["X-CLIENT-ID"] = clientId;
            }
            if (apiKey is not null)
            {
                httpContext.Request.Headers["X-API-KEY"] = apiKey;
            }

            return new AuthorizationFilterContext(
                new ActionContext(httpContext, new RouteData(), new ActionDescriptor { EndpointMetadata = endpointMetadata }),
                endpointMetadata.OfType<IFilterMetadata>().ToList());
        }

        private static (AuthorizationFilterContext Context, CurrentUserAccessor Accessor) Run(
            AuthorizationFilterContext context,
            FindApiClientDelegate? findApiClient = null,
            CurrentClaimsOptions? claimsOptions = null,
            ClientAuthenticatedDelegate? clientAuthenticated = null)
        {
            var accessor = new CurrentUserAccessor();
            var filter = new PermissionAuthorizationFilter(
                NullLogger<PermissionAuthorizationFilter>.Instance,
                accessor,
                findApiClient,
                claimsOptions,
                clientAuthenticated);

            filter.OnAuthorization(context);

            return (context, accessor);
        }

        private static ClaimsPrincipal CreatePrincipal(params (string Type, string Value)[] claims)
            => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), authenticationType: "Test"));

        private static List<object> Metadata(params object[] items) => [.. items];

        [Fact]
        public void AnonymousEndpoint_SkipsAuthorization()
        {
            var (context, accessor) = Run(BuildContext(Metadata(new AllowAnonymousAttribute())));

            Assert.Null(context.Result);
            Assert.Equal(Guid.Empty, accessor.UserId);
        }

        [Fact]
        public void NoAuthorizationHeader_Returns401Envelope()
        {
            var (context, accessor) = Run(BuildContext(Metadata()));

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
            Assert.Equal((int)HttpStatusCode.Unauthorized, context.HttpContext.Response.StatusCode);
            Assert.Equal("trace-auth-001", dto.Id);
            Assert.Equal(Guid.Empty, accessor.UserId);
        }

        [Fact]
        public void UnauthenticatedIdentity_Returns401Envelope()
        {
            // A bearer header is present but the request carries no authenticated identity.
            var (context, _) = Run(BuildContext(Metadata(), authorizationHeader: "Bearer garbage"));

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void Jwt_WithMatchingPermission_PopulatesAccessor()
        {
            var userId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var principal = CreatePrincipal(
                (BuffCoreClaimTypes.Permissions, "Finance.Payment.Read"),
                (ClaimTypes.GivenName, "Jane Doe"),
                (ClaimTypes.Email, "jane@example.com"),
                (JwtRegisteredClaimNames.Sid, userId.ToString()),
                (BuffCoreClaimTypes.CurrentCompany, companyId.ToString()));

            var (context, accessor) = Run(
                BuildContext(Metadata(new PermissionAuthorizeAttribute("Finance.Payment.Read")), principal, authorizationHeader: "Bearer test-token"));

            Assert.Null(context.Result);
            Assert.Equal(userId, accessor.UserId);
            Assert.Equal("Jane Doe", accessor.FullName);
            Assert.Equal("jane@example.com", accessor.EmailAddress);
            Assert.Equal(companyId, accessor.CompanyId);
            Assert.Equal(["Finance.Payment.Read"], accessor.Permissions);
        }

        [Fact]
        public void Jwt_WithMissingPermission_Returns401()
        {
            var principal = CreatePrincipal(
                (BuffCoreClaimTypes.Permissions, "Other.Permission"),
                (JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString()));

            var (context, accessor) = Run(
                BuildContext(Metadata(new PermissionAuthorizeAttribute("Finance.Payment.Read")), principal, authorizationHeader: "Bearer test-token"));

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
            Assert.Equal(Guid.Empty, accessor.UserId);
        }

        [Fact]
        public void Jwt_WithEmptyPermissionsAttribute_AllowsAuthenticatedUser()
        {
            var userId = Guid.NewGuid();
            var principal = CreatePrincipal(
                (BuffCoreClaimTypes.Permissions, "Anything"),
                (JwtRegisteredClaimNames.Sid, userId.ToString()));

            var (context, accessor) = Run(
                BuildContext(Metadata(new PermissionAuthorizeAttribute()), principal, authorizationHeader: "Bearer test-token"));

            Assert.Null(context.Result);
            Assert.Equal(userId, accessor.UserId);
        }

        [Fact]
        public void Jwt_WithCustomClaimsOption_ReadsOverriddenClaimType()
        {
            var userId = Guid.NewGuid();
            var principal = CreatePrincipal(
                ("scope", "Finance.Payment.Read"),
                ("user_id", userId.ToString()));

            var (context, accessor) = Run(
                BuildContext(Metadata(new PermissionAuthorizeAttribute("Finance.Payment.Read")), principal, authorizationHeader: "Bearer test-token"),
                claimsOptions: new CurrentClaimsOptions
                {
                    PermissionsClaimType = "scope",
                    UserIdClaimType = "user_id",
                });

            Assert.Null(context.Result);
            Assert.Equal(userId, accessor.UserId);
            Assert.Equal(["Finance.Payment.Read"], accessor.Permissions);
        }

        [Fact]
        public void ApiKey_WithValidClient_PopulatesAccessorAndCallsCallback()
        {
            var clientUserId = Guid.NewGuid();
            ApiPrincipal? authenticated = null;

            var (context, accessor) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-a",
                    apiKey: "secret-key-123"),
                findApiClient: id => new ApiPrincipal
                {
                    Id = clientUserId,
                    ClientId = id,
                    Name = "Partner A",
                    ApiKeyHash = HashHelper.ComputeSha256("secret-key-123"),
                    Enabled = true,
                },
                clientAuthenticated: p => authenticated = p);

            Assert.Null(context.Result);
            Assert.Equal(clientUserId, accessor.UserId);
            Assert.Equal("Partner A", accessor.FullName);
            Assert.Equal("partner-a@integration", accessor.EmailAddress);
            Assert.Empty(accessor.Permissions!);
            Assert.NotNull(authenticated);
            Assert.Equal("partner-a", authenticated!.ClientId);
        }

        [Fact]
        public void ApiKey_WithUnknownClient_Returns401()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "ghost",
                    apiKey: "whatever"),
                findApiClient: _ => null);

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void ApiKey_WithDisabledClient_Returns401()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-b",
                    apiKey: "secret-key-123"),
                findApiClient: _ => new ApiPrincipal
                {
                    ClientId = "partner-b",
                    ApiKeyHash = HashHelper.ComputeSha256("secret-key-123"),
                    Enabled = false,
                });

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void ApiKey_WithExpiredClient_Returns401()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-c",
                    apiKey: "secret-key-123"),
                findApiClient: _ => new ApiPrincipal
                {
                    ClientId = "partner-c",
                    ApiKeyHash = HashHelper.ComputeSha256("secret-key-123"),
                    Enabled = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(-1),
                });

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void ApiKey_WithWrongKey_Returns401()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-d",
                    apiKey: "wrong-key"),
                findApiClient: _ => new ApiPrincipal
                {
                    ClientId = "partner-d",
                    ApiKeyHash = HashHelper.ComputeSha256("secret-key-123"),
                    Enabled = true,
                });

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void ApiKey_WithMissingHeaders_Returns401WithoutCallingLookup()
        {
            var lookupCalled = false;

            var (context, _) = Run(
                BuildContext(Metadata(new PermissionAuthorizeApiKeyAttribute())),
                findApiClient: _ => { lookupCalled = true; return null; });

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
            Assert.False(lookupCalled);
        }

        [Fact]
        public void ApiKey_WithoutConfiguredLookup_Returns401()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-e",
                    apiKey: "secret-key-123"));

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.Unauthorized, dto.Code);
        }

        [Fact]
        public void ThrowingApiClientLookup_Produces500Envelope()
        {
            var (context, _) = Run(
                BuildContext(
                    Metadata(new PermissionAuthorizeApiKeyAttribute()),
                    clientId: "partner-f",
                    apiKey: "secret-key-123"),
                findApiClient: _ => throw new InvalidOperationException("storage failure"));

            var dto = Assert.IsType<BaseDto>(Assert.IsType<JsonResult>(context.Result).Value);
            Assert.Equal((int)HttpStatusCode.InternalServerError, dto.Code);
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.HttpContext.Response.StatusCode);
            Assert.Equal("trace-auth-001", dto.Id);
        }
    }
}
