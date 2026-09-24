using BuffCore.Web.Server.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Serilog;
using System.Text;

namespace BuffCore.Web.Server
{
    /// <summary>
    /// Extension methods for configuring the common API server stack on a
    /// <see cref="WebApplicationBuilder"/>: controllers with Newtonsoft.Json, Swagger with a
    /// Bearer security scheme, CORS, and JWT bearer authentication.
    /// </summary>
    public static class WebServerExtensions
    {
        /// <summary>
        /// Adds and configures controllers for the application with custom JSON serialization settings.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> instance being extended.</param>
        /// <returns>The updated <see cref="WebApplicationBuilder"/> instance.</returns>
        public static WebApplicationBuilder AddBuffCoreControllers(this WebApplicationBuilder builder)
        {
            // Configure antiforgery settings to suppress the default X-Frame-Options header.
            builder.Services.AddAntiforgery(options =>
            {
                options.SuppressXFrameOptionsHeader = true;
            });

            // Add controllers and configure Newtonsoft.Json (Json.NET) to handle reference loops and nulls.
            builder.Services
                .AddRouting(x =>
                {
                    x.LowercaseUrls = true;
                    x.LowercaseQueryStrings = true;
                })
                .AddControllers()
                .AddNewtonsoftJson(x =>
                {
                    x.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    x.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                });

            Log.Logger.Information("Controllers and JSON settings configured.");
            return builder;
        }

        /// <summary>
        /// Adds and configures Swagger for API documentation with JWT Bearer authentication support.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> instance being extended.</param>
        /// <param name="title">The document title shown in the Swagger UI.</param>
        /// <param name="version">The API version documented. Defaults to <c>v1</c>.</param>
        /// <returns>The updated <see cref="WebApplicationBuilder"/> instance.</returns>
        public static WebApplicationBuilder AddBuffCoreSwaggerGen(this WebApplicationBuilder builder, string title, string version = "v1")
        {
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc(version, new() { Title = title, Version = version });

                // Define the Bearer token scheme for Swagger UI
                c.AddSecurityDefinition("Bearer", new()
                {
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "Bearer",
                    In = ParameterLocation.Header,
                    Name = Microsoft.Net.Http.Headers.HeaderNames.Authorization,
                    Description = "JWT Authorization header using the Bearer scheme (Example: 'Bearer 12345abcdef')"
                });

                // Apply the Bearer token requirement globally
                c.AddSecurityRequirement(new()
                {
                    {
                        new()
                        {
                            Reference = new()
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                        },
                        []
                    }
                });

                // Enable support for Swagger annotations (e.g., [SwaggerOperation])
                c.EnableAnnotations();
            });

            return builder;
        }

        /// <summary>
        /// Configures Cross-Origin Resource Sharing (CORS) using specified allowed origins.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> instance being extended.</param>
        /// <param name="corsOrigin">An array of allowed origins. If null or empty, all origins are denied.</param>
        /// <returns>The updated <see cref="WebApplicationBuilder"/> instance.</returns>
        public static WebApplicationBuilder AddBuffCoreCors(this WebApplicationBuilder builder, string[]? corsOrigin)
        {
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(corsBuilder =>
                    corsBuilder
                        .WithOrigins(corsOrigin ?? [])
                        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                        .WithHeaders("Content-Type", "Authorization", "X-Requested-With", "X-Platform", "Cache-Control")
                        .AllowCredentials()
                );
            });

            Log.Logger.Information("CORS configured for origins: {Origins}", string.Join(", ", corsOrigin ?? []));
            return builder;
        }

        /// <summary>
        /// Configures JWT-based authentication and authorization using the supplied typed settings.
        /// </summary>
        /// <param name="builder">The <see cref="WebApplicationBuilder"/> instance being extended.</param>
        /// <param name="config">JWT issuer/audience/signing-key settings mapped from host configuration.</param>
        /// <returns>The updated <see cref="WebApplicationBuilder"/> instance.</returns>
        public static WebApplicationBuilder AddBuffCoreJwtBearer(this WebApplicationBuilder builder, JwtBearerConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = config.Issuer,
                        ValidAudience = config.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SecretKey)),
                        ClockSkew = config.ClockSkew,
                    };
                });

            builder.Services.AddAuthorization();

            Log.Logger.Information("JWT Authentication and Authorization configured.");
            return builder;
        }
    }
}
