using BuffCore.Integrations.Configurations;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BuffCore.Integrations.Tests
{
    /// <summary>
    /// Shared hermetic infrastructure: service-account credentials generated in memory
    /// (real PKCS#8 PEM, no network, no secrets on disk) and Firebase app cleanup.
    /// </summary>
    internal static class TestInfrastructure
    {
        public const string AppNamePrefix = "buffcore-fcm-";

        /// <summary>
        /// Builds a syntactically valid service account whose key is generated in-memory.
        /// The SDK imports the key at construction; no token exchange happens in unit tests.
        /// </summary>
        public static GoogleServiceAccount CreateServiceAccount()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);

            return new GoogleServiceAccount
            {
                Type = "service_account",
                PrivateKey = rsa.ExportPkcs8PrivateKeyPem(),
                ClientEmail = "buffcore-tests@example.iam.gserviceaccount.com",
            };
        }

        public static StorageConfig CreateStorageConfig() => new()
        {
            GoogleCloudStorage = new GoogleCloudStorage
            {
                ProjectId = "test-project",
                BucketName = "test-bucket",
                ServiceAccount = CreateServiceAccount(),
            }
        };

        public static MessagingConfig CreateMessagingConfig() => new()
        {
            FirebaseMessaging = new FirebaseMessaging
            {
                ProjectId = "test-project",
                ServiceAccount = CreateServiceAccount(),
            }
        };

        /// <summary>
        /// Deletes every Firebase app created by these tests, keeping the SDK's static
        /// registry clean between tests. FirebaseAdmin for .NET exposes no "list apps"
        /// API, so test apps are tracked by name here.
        /// </summary>
        public static readonly List<string> CreatedAppNames = new();

        public static void DeleteTestFirebaseApps()
        {
            foreach (var name in CreatedAppNames)
            {
                try
                {
                    FirebaseApp.GetInstance(name).Delete();
                }
                catch (ArgumentException)
                {
                    // Already deleted — fine.
                }
            }

            CreatedAppNames.Clear();
        }
    }

    public class GoogleCloudStorageHelperTests
    {
        [Fact]
        public void Constructor_WithMissingServiceAccount_Throws()
        {
            var config = new StorageConfig
            {
                GoogleCloudStorage = new GoogleCloudStorage
                {
                    ProjectId = "p",
                    BucketName = "b",
                    ServiceAccount = new GoogleServiceAccount()
                }
            };

            var exception = Assert.Throws<InvalidOperationException>(() => new GoogleCloudStorageHelper(config));

            Assert.Contains("service account", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Constructor_WithValidServiceAccount_Succeeds()
        {
            var helper = new GoogleCloudStorageHelper(TestInfrastructure.CreateStorageConfig());

            Assert.NotNull(helper);
        }

        [Fact]
        public void Constructor_AcceptsLoggerWithoutThrowing()
        {
            var helper = new GoogleCloudStorageHelper(
                TestInfrastructure.CreateStorageConfig(),
                NullLogger.Instance);

            Assert.NotNull(helper);
        }

        private sealed class NullLogger : ILogger
        {
            public static readonly NullLogger Instance = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }
    }

    public class FirebaseMessagingHelperTests : IDisposable
    {
        public FirebaseMessagingHelperTests()
        {
            TestInfrastructure.DeleteTestFirebaseApps();
        }

        public void Dispose()
        {
            TestInfrastructure.DeleteTestFirebaseApps();
        }

        [Fact]
        public void Constructor_WithMissingServiceAccount_Throws()
        {
            var config = new MessagingConfig
            {
                FirebaseMessaging = new FirebaseMessaging
                {
                    ProjectId = "p",
                    ServiceAccount = new GoogleServiceAccount()
                }
            };

            Assert.Throws<InvalidOperationException>(() => new FirebaseMessagingHelper(config));
        }

        [Fact]
        public void Constructor_WithValidServiceAccount_CreatesNamedApp()
        {
            var appName = $"{TestInfrastructure.AppNamePrefix}{Guid.NewGuid():N}";
            TestInfrastructure.CreatedAppNames.Add(appName);

            var helper = new FirebaseMessagingHelper(TestInfrastructure.CreateMessagingConfig(), firebaseAppName: appName);

            Assert.NotNull(helper);
            // Resolving the app by its expected name proves the named app was created.
            var app = FirebaseApp.GetInstance(appName);
            Assert.Equal(appName, app.Name);
        }

        [Fact]
        public void TwoInstances_CreateDistinctNamedApps()
        {
            var firstName = $"{TestInfrastructure.AppNamePrefix}{Guid.NewGuid():N}";
            var secondName = $"{TestInfrastructure.AppNamePrefix}{Guid.NewGuid():N}";
            TestInfrastructure.CreatedAppNames.Add(firstName);
            TestInfrastructure.CreatedAppNames.Add(secondName);

            var first = new FirebaseMessagingHelper(TestInfrastructure.CreateMessagingConfig(), firebaseAppName: firstName);
            var second = new FirebaseMessagingHelper(TestInfrastructure.CreateMessagingConfig(), firebaseAppName: secondName);

            var firstApp = FirebaseApp.GetInstance(firstName);
            var secondApp = FirebaseApp.GetInstance(secondName);

            Assert.NotSame(firstApp, secondApp);
        }
    }

    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddBuffCoreIntegrations_RegistersBothHelpersAsSingletons()
        {
            var services = new ServiceCollection();

            services.AddBuffCoreIntegrations(TestInfrastructure.CreateStorageConfig(), TestInfrastructure.CreateMessagingConfig());

            using var provider = services.BuildServiceProvider();
            var first = provider.GetRequiredService<GoogleCloudStorageHelper>();
            var second = provider.GetRequiredService<GoogleCloudStorageHelper>();
            Assert.Same(first, second);

            var firstFcm = provider.GetRequiredService<FirebaseMessagingHelper>();
            var secondFcm = provider.GetRequiredService<FirebaseMessagingHelper>();
            Assert.Same(firstFcm, secondFcm);
        }

        [Fact]
        public void AddBuffCoreIntegrations_WithNullStorageConfig_SkipsStorageHelper()
        {
            var services = new ServiceCollection();

            services.AddBuffCoreIntegrations(null, TestInfrastructure.CreateMessagingConfig());

            using var provider = services.BuildServiceProvider();
            Assert.Null(provider.GetService<GoogleCloudStorageHelper>());
            Assert.NotNull(provider.GetService<FirebaseMessagingHelper>());
        }

        [Fact]
        public void AddBuffCoreIntegrations_WithNullMessagingConfig_SkipsMessagingHelper()
        {
            var services = new ServiceCollection();

            services.AddBuffCoreIntegrations(TestInfrastructure.CreateStorageConfig(), null);

            using var provider = services.BuildServiceProvider();
            Assert.NotNull(provider.GetService<GoogleCloudStorageHelper>());
            Assert.Null(provider.GetService<FirebaseMessagingHelper>());
        }

        [Fact]
        public void AddBuffCoreIntegrations_WithBothNull_RegistersNothingAndDoesNotThrow()
        {
            var services = new ServiceCollection();

            services.AddBuffCoreIntegrations(null, null);

            using var provider = services.BuildServiceProvider();
            Assert.Null(provider.GetService<GoogleCloudStorageHelper>());
            Assert.Null(provider.GetService<FirebaseMessagingHelper>());
        }
    }
}
