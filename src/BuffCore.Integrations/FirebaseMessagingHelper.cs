using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using BuffCore.Integrations.Configurations;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;

namespace BuffCore.Integrations
{
    /// <summary>
    /// Injectable helper for sending notifications via Firebase Cloud Messaging (FCM).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construct with a <see cref="MessagingConfig"/> (or register via
    /// <see cref="ServiceCollectionExtensions.AddBuffCoreIntegrations"/>) and inject where needed.
    /// The <see cref="FirebaseApp"/> is created from the supplied service account, so the host never
    /// touches <c>FirebaseApp.Create</c> or global SDK state directly.
    /// </para>
    /// <para>
    /// Each helper instance owns a <em>named</em> <see cref="FirebaseApp"/> (never the global default
    /// instance), so multiple host contexts — or multiple helper instances — cannot collide on
    /// FirebaseAdmin's static app registry.
    /// </para>
    /// <para>
    /// FirebaseAdmin resolves messages through the static <c>FirebaseMessaging.DefaultInstance</c>,
    /// so send operations are exercised through the real SDK (integration-tested). Failure to
    /// initialize propagates at construction time rather than being swallowed.
    /// </para>
    /// </remarks>
    /// <param name="config">The Firebase messaging settings, including the service account.</param>
    /// <param name="logger">An optional logger; defaults to a no-op logger.</param>
    /// <param name="firebaseAppName">
    /// An optional deterministic Firebase app name. Defaults to a unique generated name, which is
    /// correct for production; tests may supply a fixed name to assert or clean up the app.
    /// </param>
    public class FirebaseMessagingHelper(MessagingConfig config, ILogger? logger = null, string? firebaseAppName = null)
    {
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        private readonly FirebaseAdmin.Messaging.FirebaseMessaging _messaging = CreateMessaging(config, firebaseAppName);

        private static FirebaseAdmin.Messaging.FirebaseMessaging CreateMessaging(MessagingConfig config, string? firebaseAppName)
        {
            var serviceAccount = config.FirebaseMessaging.ServiceAccount;

            if (string.IsNullOrWhiteSpace(serviceAccount?.Type) || string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
            {
                throw new InvalidOperationException(
                    "Firebase Messaging configuration is missing a service account (type and private_key are required).");
            }

            // FCM resolves the project id from AppOptions or the credential's project_id field. The
            // BuffCore service-account shape intentionally does not carry project_id (the credential
            // file has more fields than we model), so the configured ProjectId is passed explicitly.
            if (string.IsNullOrWhiteSpace(config.FirebaseMessaging.ProjectId))
            {
                throw new InvalidOperationException(
                    "Firebase Messaging configuration is missing the project id, which FCM requires.");
            }

            // A unique app name per helper instance keeps FirebaseAdmin's static registry free of
            // collisions between multiple instances (hosts, tests) that each bring their own credentials.
            var app = FirebaseApp.Create(
                new AppOptions()
                {
                    Credential = CredentialFactory.FromJson(
                        Newtonsoft.Json.JsonConvert.SerializeObject(serviceAccount),
                        serviceAccount.Type),
                    ProjectId = config.FirebaseMessaging.ProjectId,
                },
                firebaseAppName ?? $"buffcore-fcm-{Guid.NewGuid():N}");

            return FirebaseAdmin.Messaging.FirebaseMessaging.GetMessaging(app);
        }

        /// <summary>
        /// Sends a notification message to a single device using its FCM token.
        /// </summary>
        /// <param name="fcmToken">The target device's FCM token.</param>
        /// <param name="title">The notification's title.</param>
        /// <param name="body">The notification's body text.</param>
        /// <param name="data">Optional custom data payload.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The message Id string if the message was successfully sent.</returns>
        public async Task<string> SendToTokenAsync(string fcmToken, string title, string body, IDictionary<string, string>? data = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Sending notification to token {FcmToken} with title '{Title}'.", fcmToken, title);

            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = new ReadOnlyDictionary<string, string>(data ?? new Dictionary<string, string>())
            };

            var response = await _messaging.SendAsync(message, cancellationToken);

            _logger.LogInformation("Notification sent successfully to token {FcmToken}. Message ID: {MessageId}", fcmToken, response);
            return response;
        }

        /// <summary>
        /// Sends a multicast notification message to multiple devices using their FCM tokens.
        /// </summary>
        /// <param name="fcmTokens">Collection of target device FCM tokens.</param>
        /// <param name="title">The notification's title.</param>
        /// <param name="body">The notification's body text.</param>
        /// <param name="data">Optional custom data payload.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="BatchResponse"/> with the send results for each token.</returns>
        public async Task<BatchResponse> SendMulticastAsync(IEnumerable<string> fcmTokens, string title, string body, IDictionary<string, string>? data = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Sending multicast notification to {TokenCount} tokens with title '{Title}'.", fcmTokens.Count(), title);

            var multicastMessage = new MulticastMessage
            {
                Tokens = [.. fcmTokens],
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = new ReadOnlyDictionary<string, string>(data ?? new Dictionary<string, string>())
            };

            var response = await _messaging.SendEachForMulticastAsync(multicastMessage, cancellationToken);

            _logger.LogInformation("Multicast notification sent. Success count: {SuccessCount}, Failure count: {FailureCount}", response.SuccessCount, response.FailureCount);
            return response;
        }

        /// <summary>
        /// Sends a data-only message to a single device using its FCM token.
        /// </summary>
        /// <param name="fcmToken">The target device's FCM token.</param>
        /// <param name="data">Custom key-value pairs to send in the data payload.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The message Id string if the message was successfully sent.</returns>
        public async Task<string> SendDataOnlyToTokenAsync(string fcmToken, IDictionary<string, string> data, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sending data-only message to token {FcmToken}.", fcmToken);

            var message = new Message
            {
                Token = fcmToken,
                Data = new ReadOnlyDictionary<string, string>(data)
            };

            var response = await _messaging.SendAsync(message, cancellationToken);

            _logger.LogInformation("Data-only message sent successfully to token {FcmToken}. Message ID: {MessageId}", fcmToken, response);
            return response;
        }
    }
}
