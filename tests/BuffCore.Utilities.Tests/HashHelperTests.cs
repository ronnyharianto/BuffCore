using Xunit;

namespace BuffCore.Utilities.Tests
{
    public class HashHelperTests
    {
        [Fact]
        public void ComputeSha256_ReturnsUppercaseHex()
        {
            var result = HashHelper.ComputeSha256("hello");

            Assert.Equal("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824", result);
        }

        [Fact]
        public void ComputeSha256_MatchesKnownVector()
        {
            var result = HashHelper.ComputeSha256("abc");

            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", result);
        }

        [Fact]
        public void ComputeSha256_IsDeterministic()
        {
            var first = HashHelper.ComputeSha256("same-input");
            var second = HashHelper.ComputeSha256("same-input");

            Assert.Equal(first, second);
        }

        [Fact]
        public void ComputeSha256_Base64Format_ReturnsBase64()
        {
            var hex = HashHelper.ComputeSha256("abc");
            var base64 = HashHelper.ComputeSha256("abc", "base64");

            var expected = Convert.ToBase64String(Convert.FromHexString(hex));
            Assert.Equal(expected, base64);
        }

        [Fact]
        public void ComputeSha256_UnknownFormat_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HashHelper.ComputeSha256("abc", "md5"));
        }
    }
}
