using BuffCore.Integrations.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuffCore.Integrations
{
    /// <summary>
    /// DI registration for the cloud-integration helpers.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="GoogleCloudStorageHelper"/> and <see cref="FirebaseMessagingHelper"/>
        /// as singletons constructed from the supplied configuration instances.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
        /// <param name="storageConfig">
        /// The storage settings; pass null to leave <see cref="GoogleCloudStorageHelper"/> unregistered
        /// (e.g. when storage is not used in the current environment).
        /// </param>
        /// <param name="messagingConfig">
        /// The messaging settings; pass null to leave <see cref="FirebaseMessagingHelper"/> unregistered
        /// (e.g. when push notifications are not used in the current environment).
        /// </param>
        /// <returns>The updated <see cref="IServiceCollection"/> instance for chaining.</returns>
        public static IServiceCollection AddBuffCoreIntegrations(
            this IServiceCollection services,
            StorageConfig? storageConfig,
            MessagingConfig? messagingConfig)
        {
            if (storageConfig != null)
            {
                services.AddSingleton(_ => new GoogleCloudStorageHelper(storageConfig));
            }

            if (messagingConfig != null)
            {
                services.AddSingleton(_ => new FirebaseMessagingHelper(messagingConfig));
            }

            return services;
        }
    }
}
