using BuffCore.Web.Server.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using Xunit;

namespace BuffCore.Web.Server.Tests
{
    /// <summary>
    /// Verifies the <see cref="WebServerExtensions"/> registrations, especially that
    /// <see cref="JwtBearerConfig"/> values reach <c>TokenValidationParameters</c>.
    /// Options are resolved through <see cref="IOptionsMonitor{TOptions}"/>/<see cref="IOptionsSnapshot{TOptions}"/>
    /// so that post-configure hooks registered by the extensions are applied before assertion.
    /// </summary>
    public class RegistrationTests
    {
        [Fact]
        public async Task AddBuffCoreJwtBearer_MapsConfigToTokenValidationParameters()
        {
            var builder = WebApplication.CreateBuilder();
            var config = new JwtBearerConfig
            {
                Issuer = "https://issuer.example.com",
                Audience = "Test Audience",
                SecretKey = "a-sufficiently-long-test-signing-key-value",
                ClockSkew = TimeSpan.FromMinutes(1),
            };

            builder.AddBuffCoreJwtBearer(config);

            await using var app = builder.Build();
            var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();
            var scheme = await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme);

            Assert.NotNull(scheme);

            var parameters = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme)
                .TokenValidationParameters;

            Assert.Equal(config.Issuer, parameters.ValidIssuer);
            Assert.Equal(config.Audience, parameters.ValidAudience);
            Assert.Equal(config.ClockSkew, parameters.ClockSkew);
            Assert.True(parameters.ValidateIssuer);
            Assert.True(parameters.ValidateAudience);
            Assert.True(parameters.ValidateLifetime);
            Assert.True(parameters.ValidateIssuerSigningKey);

            var key = Assert.IsType<SymmetricSecurityKey>(parameters.IssuerSigningKey);
            Assert.Equal(Encoding.UTF8.GetBytes(config.SecretKey), key.Key);
        }

        [Fact]
        public void AddBuffCoreControllers_RegistersLowercaseRoutingAndNewtonsoftFormatters()
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddBuffCoreControllers();

            using var provider = builder.Services.BuildServiceProvider();

            var routeOptions = provider.GetRequiredService<IOptionsMonitor<RouteOptions>>().CurrentValue;
            Assert.True(routeOptions.LowercaseUrls);
            Assert.True(routeOptions.LowercaseQueryStrings);

            var mvcOptions = provider.GetRequiredService<IOptionsMonitor<MvcOptions>>().CurrentValue;
            Assert.Contains(mvcOptions.OutputFormatters, f => f is NewtonsoftJsonOutputFormatter);
        }

        [Fact]
        public void AddBuffCoreCors_RegistersDefaultPolicyWithConfiguredOrigins()
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddBuffCoreCors(["https://app.example.com"]);

            var corsOptions = builder.Services.BuildServiceProvider()
                .GetRequiredService<IOptionsMonitor<CorsOptions>>().CurrentValue;
            var policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);

            Assert.NotNull(policy);
            Assert.Contains("https://app.example.com", policy!.Origins);
            Assert.True(policy.SupportsCredentials);
        }

        [Fact]
        public void AddBuffCoreCors_WithNoOrigins_RegistersEmptyDefaultPolicy()
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddBuffCoreCors(null);

            var corsOptions = builder.Services.BuildServiceProvider()
                .GetRequiredService<IOptionsMonitor<CorsOptions>>().CurrentValue;
            var policy = corsOptions.GetPolicy(corsOptions.DefaultPolicyName);

            Assert.NotNull(policy);
            Assert.Empty(policy!.Origins);
        }

        [Fact]
        public void AddBuffCoreSwaggerGen_RegistersDocumentWithTitleAndVersion()
        {
            var builder = WebApplication.CreateBuilder();

            builder.AddBuffCoreSwaggerGen("Test.API", "v9");

            using var provider = builder.Services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptionsSnapshot<SwaggerGenOptions>>().Value;

            Assert.Contains("v9", options.SwaggerGeneratorOptions.SwaggerDocs.Keys);
            Assert.Equal("Test.API", options.SwaggerGeneratorOptions.SwaggerDocs["v9"].Title);
        }

        [Fact]
        public void AddBuffCoreJwtBearer_WithNullConfig_Throws()
        {
            var builder = WebApplication.CreateBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.AddBuffCoreJwtBearer(null!));
        }
    }
}
