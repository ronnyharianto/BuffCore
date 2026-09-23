using System.Security.Cryptography;
using System.Text;

namespace BuffCore.Utilities
{
    /// <summary>
    /// Provides SHA256 hashing utilities for deterministic hashing of text input
    /// (e.g. API key hashes, checksums, or cache keys).
    /// </summary>
    /// <remarks>
    /// This helper is intentionally stateless; a single call computes the hash and returns.
    /// For password storage and verification, use a dedicated password hasher
    /// (e.g. <c>Microsoft.AspNetCore.Identity.PasswordHasher&lt;TUser&gt;</c>) instead.
    /// </remarks>
    public static class HashHelper
    {
        /// <summary>
        /// Computes the SHA256 hash of the specified input string.
        /// </summary>
        /// <param name="input">The input string to hash (UTF-8 encoded).</param>
        /// <returns>The computed SHA256 hash as an uppercase hexadecimal string.</returns>
        public static string ComputeSha256(string input)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

            return Convert.ToHexString(hashBytes);
        }

        /// <summary>
        /// Computes the SHA256 hash of the specified input string.
        /// </summary>
        /// <param name="input">The input string to hash (UTF-8 encoded).</param>
        /// <param name="format">The output format: "hex" (uppercase, default) or "base64".</param>
        /// <returns>The computed SHA256 hash in the requested encoding.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an unknown format is requested.</exception>
        public static string ComputeSha256(string input, string format)
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));

            return format.ToLowerInvariant() switch
            {
                "hex" => Convert.ToHexString(hashBytes),
                "base64" => Convert.ToBase64String(hashBytes),
                _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown hash output format."),
            };
        }
    }
}
