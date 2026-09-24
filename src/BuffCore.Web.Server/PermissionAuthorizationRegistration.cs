using BuffCore.Abstractions;
using BuffCore.Web.Server.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuffCore.Web.Server
{
    /// <summary>
    /// Registration helpers for <see cref="PermissionAuthorizationFilter"/>.
    /// </summary>
    public static class PermissionAuthorizationRegistration
    {
        /// <summary>
        /// Registers <see cref="PermissionAuthorizationFilter"/> as a global MVC authorization filter.
        /// Call after <c>AddBuffCoreControllers</c> (or any other MVC registration). Delegate factories
        /// receive the request's <see cref="IServiceProvider"/>, so scoped services (e.g. the host's
        /// <c>DbContext</c>) may be captured for use inside the delegates.
        /// </summary>
        /// <param name="services">The service collection to register into.</param>
        /// <param name="findApiClientFactory">
        /// Optional factory producing the delegate that resolves an <see cref="ApiPrincipal"/> by its
        /// public <c>X-CLIENT-ID</c> value; required only for endpoints marked with
        /// <c>PermissionAuthorizeApiKeyAttribute</c>.
        /// </param>
        /// <param name="clientAuthenticatedFactory">
        /// Optional factory producing a callback invoked after successful API-key authentication
        /// (e.g. to stamp a <c>LastUsedAt</c> timestamp).
        /// </param>
        /// <param name="claimsOptions">
        /// Optional claim-name overrides for JWT claim mapping; defaults match the BuffCore claim
        /// convention.
        /// </param>
        /// <returns>The updated <see cref="IServiceCollection"/> instance for chaining.</returns>
        public static IServiceCollection AddPermissionAuthorizationFilter(
            this IServiceCollection services,
            Func<IServiceProvider, FindApiClientDelegate>? findApiClientFactory = null,
            Func<IServiceProvider, ClientAuthenticatedDelegate>? clientAuthenticatedFactory = null,
            CurrentClaimsOptions? claimsOptions = null)
        {
            services.AddScoped(sp => new PermissionAuthorizationFilter(
                sp.GetRequiredService<ILogger<PermissionAuthorizationFilter>>(),
                sp.GetRequiredService<CurrentUserAccessor>(),
                findApiClientFactory?.Invoke(sp),
                claimsOptions,
                clientAuthenticatedFactory?.Invoke(sp)));

            services.Configure<MvcOptions>(options => options.Filters.Add<PermissionAuthorizationFilter>());

            return services;
        }
    }
}
