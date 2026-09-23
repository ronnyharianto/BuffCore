namespace BuffCore.Utilities.Objects
{
    /// <summary>
    /// Represents an email address with an optional display name.
    /// </summary>
    /// <remarks>
    /// Keeps <see cref="EmailMessage"/> free of <c>System.Net.Mail</c> types so callers
    /// can construct messages without SMTP-specific dependencies.
    /// </remarks>
    public class EmailAddress
    {
        /// <summary>
        /// Creates an email address.
        /// </summary>
        /// <param name="email">The email address (e.g. "user@example.com").</param>
        /// <param name="displayName">The optional display name (e.g. "Jane Doe").</param>
        public EmailAddress(string email, string? displayName = null)
        {
            Email = email;
            DisplayName = displayName ?? string.Empty;
        }

        /// <summary>
        /// The email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The optional display name shown to recipients.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
