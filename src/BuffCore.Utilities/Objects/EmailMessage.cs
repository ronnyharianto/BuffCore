using BuffCore.Utilities.Configurations;

namespace BuffCore.Utilities.Objects
{
    /// <summary>
    /// Describes an email to send: sender, recipients, subject, and HTML body.
    /// </summary>
    /// <remarks>
    /// Carries no SMTP credentials — the password is supplied separately via
    /// <see cref="Configurations.SmtpConfig"/>, keeping credentials out of message objects.
    /// </remarks>
    public class EmailMessage
    {
        /// <summary>
        /// The sender's email address.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// The sender's display name.
        /// </summary>
        public string FromDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// The message subject.
        /// </summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// The message body (HTML).
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// A single primary recipient.
        /// </summary>
        public EmailAddress? To { get; set; }

        /// <summary>
        /// Multiple recipients; when set, takes precedence over <see cref="To"/>.
        /// </summary>
        public List<EmailAddress> MultiRecipients { get; set; } = [];
    }
}
