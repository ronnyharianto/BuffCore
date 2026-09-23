using System.Net.Mail;
using BuffCore.Utilities.Configurations;
using BuffCore.Utilities.Objects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class EmailHelperTests
    {
        /// <summary>
        /// Builds an <see cref="EmailHelper"/> whose SMTP transport writes .eml files to a
        /// pickup directory instead of opening network connections — exercising the real
        /// SmtpClient pipeline while keeping tests hermetic.
        /// </summary>
        private static EmailHelper CreateHelper(string pickupDirectory)
            => new(
                NullLogger<EmailHelper>.Instance,
                smtpClientFactory: _ => new SmtpClient
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = pickupDirectory,
                });

        [Fact]
        public async Task SendAsync_WithoutConfig_ReturnsFalse()
        {
            var helper = new EmailHelper(NullLogger<EmailHelper>.Instance);

            var result = await helper.SendAsync(BuildMessage());

            Assert.False(result);
        }

        [Fact]
        public async Task SendAsync_WithPerCallConfig_ReturnsTrue()
        {
            var helper = CreateHelper(NewPickupDirectory());

            var result = await helper.SendAsync(BuildMessage(), new SmtpConfig { Host = "smtp.test.local", Port = 25 });

            Assert.True(result);
        }

        [Fact]
        public async Task SendAsync_WithConfiguredDefaults_ReturnsTrue()
        {
            var helper = CreateHelper(NewPickupDirectory());
            helper.Configure(new SmtpConfig { Host = "smtp.test.local", Port = 587 });

            var result = await helper.SendAsync(BuildMessage());

            Assert.True(result);
        }

        [Fact]
        public async Task SendAsync_MultiRecipients_ReturnsTrue()
        {
            var helper = CreateHelper(NewPickupDirectory());
            var message = BuildMessage();
            message.MultiRecipients =
            [
                new EmailAddress("a@example.com", "A"),
                new EmailAddress("b@example.com", "B"),
            ];

            var result = await helper.SendAsync(message, new SmtpConfig { Host = "smtp.test.local", Port = 25 });

            Assert.True(result);
        }

        [Fact]
        public async Task SendAsync_MapsSenderSubjectAndBody()
        {
            var directory = NewPickupDirectory();
            var helper = CreateHelper(directory);

            await helper.SendAsync(BuildMessage(), new SmtpConfig { Host = "smtp.test.local", Port = 25 });

            var eml = ReadEml(directory);
            Assert.Contains("noreply@example.com", eml);
            Assert.Contains("a@example.com", eml);
            Assert.Contains("Subject: Subject", eml);
            Assert.Contains("<p>Body</p>", eml);
        }

        [Fact]
        public void EmailMessage_DoesNotCarryCredentials()
        {
            var message = BuildMessage();

            Assert.DoesNotContain(typeof(EmailMessage).GetProperties(), p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task SendAsync_PassesSmtpConfigToTransportFactory()
        {
            var directory = NewPickupDirectory();
            SmtpConfig? received = null;
            var helper = new EmailHelper(
                NullLogger<EmailHelper>.Instance,
                smtpClientFactory: cfg =>
                {
                    received = cfg;
                    return new SmtpClient
                    {
                        DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                        PickupDirectoryLocation = directory,
                    };
                });

            await helper.SendAsync(BuildMessage(), new SmtpConfig { Host = "smtp.test.local", Port = 587, UserName = "user", Password = "secret" });

            Assert.NotNull(received);
            Assert.Equal("user", received!.UserName);
            Assert.Equal("secret", received.Password);
            Assert.Equal("smtp.test.local", received.Host);
        }

        private static EmailMessage BuildMessage()
            => new()
            {
                From = "noreply@example.com",
                FromDisplayName = "Example",
                Subject = "Subject",
                Body = "<p>Body</p>",
                To = new EmailAddress("a@example.com", "A"),
            };

        private static string NewPickupDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "buffcore-email-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string ReadEml(string directory)
        {
            var files = Directory.GetFiles(directory, "*.eml");
            Assert.NotEmpty(files);

            return File.ReadAllText(files[0]);
        }
    }
}
