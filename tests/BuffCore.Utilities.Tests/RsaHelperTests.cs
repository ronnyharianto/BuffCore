using BuffCore.Utilities.Configurations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class RsaHelperTests
    {
        private static (RsaHelper Helper, RsaConfig Config) CreateInitializedHelper()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            var config = new RsaConfig
            {
                PublicKey = rsa.ExportSubjectPublicKeyInfoPem(),
                PrivateKey = rsa.ExportPkcs8PrivateKeyPem(),
            };

            var helper = new RsaHelper(NullLogger<RsaHelper>.Instance);
            helper.Initialize(config).GetAwaiter().GetResult();

            return (helper, config);
        }

        [Fact]
        public async Task EncryptDecrypt_RoundtripsOriginalValue()
        {
            var (helper, _) = CreateInitializedHelper();

            var cipher = helper.Encrypt("secret-value");
            var plain = helper.Decrypt(cipher);

            Assert.NotEqual("secret-value", cipher);
            Assert.Equal("secret-value", plain);
        }

        [Fact]
        public async Task Initialize_IsReconfigurable()
        {
            var (helper, _) = CreateInitializedHelper();
            using var otherRsa = System.Security.Cryptography.RSA.Create(2048);
            var rotated = new RsaConfig
            {
                PublicKey = otherRsa.ExportSubjectPublicKeyInfoPem(),
                PrivateKey = otherRsa.ExportPkcs8PrivateKeyPem(),
            };

            await helper.Initialize(rotated);
            var cipher = helper.Encrypt("after-rotation");

            Assert.Equal("after-rotation", helper.Decrypt(cipher));
        }

        [Fact]
        public void Encrypt_WithoutInitialize_ReturnsEmpty()
        {
            var helper = new RsaHelper(NullLogger<RsaHelper>.Instance);

            Assert.Equal(string.Empty, helper.Encrypt("anything"));
        }

        [Fact]
        public void Decrypt_WithoutInitialize_ReturnsEmpty()
        {
            var helper = new RsaHelper(NullLogger<RsaHelper>.Instance);

            Assert.Equal(string.Empty, helper.Decrypt("AAAA"));
        }

        [Fact]
        public async Task Encrypt_WithInvalidKey_ReturnsEmptyWithoutThrowing()
        {
            var helper = new RsaHelper(NullLogger<RsaHelper>.Instance);
            await helper.Initialize(new RsaConfig { PublicKey = "not-a-pem", PrivateKey = "not-a-pem" });

            Assert.Equal(string.Empty, helper.Encrypt("anything"));
        }

        [Fact]
        public void Decrypt_WithGarbageCiphertext_ReturnsEmptyWithoutThrowing()
        {
            var (helper, _) = CreateInitializedHelper();

            Assert.Equal(string.Empty, helper.Decrypt("not-base64!!"));
        }

        [Fact]
        public async Task Initialize_NullConfig_Throws()
        {
            var helper = new RsaHelper(NullLogger<RsaHelper>.Instance);

            await Assert.ThrowsAsync<ArgumentNullException>(() => helper.Initialize(null!));
        }
    }
}
