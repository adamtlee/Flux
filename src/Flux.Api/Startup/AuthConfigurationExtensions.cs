using System.Text;
using Flux.Services.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Flux.Api.Startup;

public static class AuthConfigurationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"];
        var jwtIssuer = configuration["Jwt:Issuer"];
        var jwtAudience = configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
        {
            throw new InvalidOperationException("JWT settings are missing. Configure Jwt:Key, Jwt:Issuer, and Jwt:Audience in appsettings.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }

    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.FreeMember, policy =>
                policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.PremiumMember, ApplicationRoles.FreeMember));

            options.AddPolicy(AuthorizationPolicies.PremiumMember, policy =>
                policy.RequireRole(ApplicationRoles.Administrator, ApplicationRoles.PremiumMember));

            options.AddPolicy(AuthorizationPolicies.Administrator, policy =>
                policy.RequireRole(ApplicationRoles.Administrator));
        });

        return services;
    }
}
