using BuffCore.Utilities.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace BuffCore.Utilities
{
    /// <summary>
    /// Dependency injection registrations for BuffCore.Utilities.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers BuffCore.Utilities services: <see cref="JsonHelper"/>, <see cref="EmailHelper"/>,
        /// and <see cref="RsaHelper"/> (singletons) plus <see cref="HttpClientHelper"/> (typed HTTP client).
        /// </summary>
        /// <param name="services">Service collection to register services into.</param>
        /// <param name="httpClientConfig">Optional typed-client settings; defaults apply when null.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddBuffCoreUtilities(this IServiceCollection services, HttpClientConfig? httpClientConfig = null)
        {
            var config = httpClientConfig ?? new HttpClientConfig();

            services.TryAddSingleton(sp => new JsonHelper(sp.GetService<ILoggerFactory>()?.CreateLogger<JsonHelper>()));

            services.TryAddSingleton(sp => new EmailHelper(sp.GetService<ILoggerFactory>()?.CreateLogger<EmailHelper>()));

            services.TryAddSingleton(sp => new RsaHelper(sp.GetService<ILoggerFactory>()?.CreateLogger<RsaHelper>()));

            services.AddHttpClient<HttpClientHelper>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(config.Timeout);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(config.HandlerLifetime));

            return services;
        }
    }
}
