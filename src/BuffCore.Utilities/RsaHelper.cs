using BuffCore.Utilities.Configurations;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace BuffCore.Utilities
{
    /// <summary>
    /// RSA encryption and decryption using configured PEM keys (OAEP-SHA512, Base64 ciphertext).
    /// </summary>
    /// <remarks>
    /// <para>
    /// An instance can be reconfigured via <see cref="Initialize"/> for key rotation
    /// without re-registering in DI.
    /// </para>
    /// <para>
    /// Operations are lenient: when keys are missing or a cryptographic operation fails,
    /// the helper logs (when a logger is available) and returns <see cref="string.Empty"/>
    /// instead of throwing.
    /// </para>
    /// </remarks>
    public class RsaHelper(ILogger? logger = null)
    {
        private readonly ILogger? _logger = logger;
        private RsaConfig? _config;

        /// <summary>
        /// Initializes (or reconfigures) the helper with an RSA key pair.
        /// Required before any encrypt/decrypt call succeeds.
        /// </summary>
        /// <param name="config">The RSA key configuration.</param>
        /// <returns>A completed task for awaitability symmetry with other BuffCore helpers.</returns>
        public Task Initialize(RsaConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;

            _logger?.LogInformation("RSA helper initialized.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Encrypts a string value using the RSA public key.
        /// </summary>
        /// <param name="value">The plain text value to encrypt.</param>
        /// <returns>The encrypted value encoded in Base64; empty when not initialized or on failure.</returns>
        public string Encrypt(string value)
        {
            if (_config is null)
            {
                _logger?.LogError("RSA encrypt failed: helper is not initialized. Call Initialize first.");
                return string.Empty;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(_config.PublicKey);
                var encrypted = rsa.Encrypt(Encoding.UTF8.GetBytes(value), RSAEncryptionPadding.OaepSHA512);

                _logger?.LogInformation("Successfully encrypted data using RSA public key.");
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RSA encryption failed.");
                return string.Empty;
            }
        }

        /// <summary>
        /// Decrypts an encrypted Base64 string using the RSA private key.
        /// </summary>
        /// <param name="encryptedValue">The encrypted Base64 string.</param>
        /// <returns>The decrypted plain text; empty when not initialized or on failure.</returns>
        public string Decrypt(string encryptedValue)
        {
            if (_config is null)
            {
                _logger?.LogError("RSA decrypt failed: helper is not initialized. Call Initialize first.");
                return string.Empty;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(_config.PrivateKey);
                var decrypted = rsa.Decrypt(Convert.FromBase64String(encryptedValue), RSAEncryptionPadding.OaepSHA512);

                var value = Encoding.UTF8.GetString(decrypted);
                _logger?.LogInformation("Successfully decrypted data using RSA private key.");
                return value;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RSA decryption failed.");
                return string.Empty;
            }
        }
    }
}
