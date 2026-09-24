using BuffCore.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace BuffCore.Web.Server.Tests
{
    /// <summary>
    /// Covers claim mapping defaults and overrides of <see cref="CurrentClaimsMapper"/>.
    /// </summary>
    public class CurrentClaimsMapperTests
    {
        private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
            => new(new ClaimsIdentity(claims, authenticationType: "Test"));

        [Fact]
        public void Map_WithStandardClaims_PopulatesAccessor()
        {
            var userId = Guid.NewGuid();
            var companyId = Guid.NewGuid();

            var principal = CreatePrincipal(
                new Claim(JwtRegisteredClaimNames.Sid, userId.ToString()),
                new Claim(ClaimTypes.GivenName, "Jane Doe"),
                new Claim(ClaimTypes.Email, "jane.doe@example.com"),
                new Claim(BuffCoreClaimTypes.CurrentCompany, companyId.ToString()),
                new Claim(BuffCoreClaimTypes.Permissions, "Finance.Payment.Read"),
                new Claim(BuffCoreClaimTypes.Permissions, "Finance.Payment.Write"));

            var accessor = CurrentClaimsMapper.Map(principal);

            Assert.Equal(userId, accessor.UserId);
            Assert.Equal("Jane Doe", accessor.FullName);
            Assert.Equal("jane.doe@example.com", accessor.EmailAddress);
            Assert.Equal(companyId, accessor.CompanyId);
            Assert.Equal(["Finance.Payment.Read", "Finance.Payment.Write"], accessor.Permissions);
        }

        [Fact]
        public void Map_WithMissingClaims_FallsBackToDefaults()
        {
            var accessor = CurrentClaimsMapper.Map(CreatePrincipal());

            Assert.Equal(Guid.Empty, accessor.UserId);
            Assert.Equal(Guid.Empty, accessor.CompanyId);
            Assert.Equal(string.Empty, accessor.FullName);
            Assert.Equal(string.Empty, accessor.EmailAddress);
            Assert.Empty(accessor.Permissions!);
        }

        [Fact]
        public void Map_WithMalformedGuids_FallsBackToEmptyGuid()
        {
            var principal = CreatePrincipal(
                new Claim(JwtRegisteredClaimNames.Sid, "not-a-guid"),
                new Claim(BuffCoreClaimTypes.CurrentCompany, ""));

            var accessor = CurrentClaimsMapper.Map(principal);

            Assert.Equal(Guid.Empty, accessor.UserId);
            Assert.Equal(Guid.Empty, accessor.CompanyId);
        }

        [Fact]
        public void Map_WithCustomClaimNames_UsesOverrides()
        {
            var userId = Guid.NewGuid();
            var companyId = Guid.NewGuid();

            var principal = CreatePrincipal(
                new Claim("user_id", userId.ToString()),
                new Claim("name", "Jane Doe"),
                new Claim("mail", "jane.doe@example.com"),
                new Claim("tenant_id", companyId.ToString()),
                new Claim("scope", "MasterData.Customer.Access"));

            var accessor = CurrentClaimsMapper.Map(principal, new CurrentClaimsOptions
            {
                UserIdClaimType = "user_id",
                FullNameClaimType = "name",
                EmailAddressClaimType = "mail",
                CompanyIdClaimType = "tenant_id",
                PermissionsClaimType = "scope",
            });

            Assert.Equal(userId, accessor.UserId);
            Assert.Equal("Jane Doe", accessor.FullName);
            Assert.Equal("jane.doe@example.com", accessor.EmailAddress);
            Assert.Equal(companyId, accessor.CompanyId);
            Assert.Equal(["MasterData.Customer.Access"], accessor.Permissions);
        }

        [Fact]
        public void Map_WithUnauthenticatedIdentity_StillMapsAvailableClaims()
        {
            // No authentication type: identity exists but is not authenticated.
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sid, Guid.NewGuid().ToString())]));

            var accessor = CurrentClaimsMapper.Map(principal);

            Assert.NotEqual(Guid.Empty, accessor.UserId);
        }

        [Fact]
        public void Map_WithNullPrincipal_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CurrentClaimsMapper.Map(null!));
        }
    }
}
