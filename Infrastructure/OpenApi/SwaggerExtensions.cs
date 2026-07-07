using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;

namespace Spotster.Infrastructure.OpenApi;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSpotsterSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Spotster API",
                Version = "v1",
                Description =
                    "REST API for Spotster web and mobile clients (Android, MAUI, etc.).\n\n" +
                    "**Authentication:** register, confirm email, then login to obtain JWT access and refresh tokens. " +
                    "Send `Authorization: Bearer {accessToken}` on protected endpoints.\n\n" +
                    "**Localization:** optional header `X-Culture: it` or `en`.\n\n" +
                    "**Real-time:** SignalR hub at `/hubs/parking` (see `GET /api/app/config` for hub methods and server events)."
            });

            // ApiExplorerSettings(GroupName) is used for Swagger tags, not to filter docs by name.
            options.DocInclusionPredicate((_, _) => true);

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT access token from `POST /api/auth/login`. Example: Bearer eyJhbG..."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme
                        }
                    },
                    Array.Empty<string>()
                }
            });

            options.TagActionsBy(api =>
            {
                if (!string.IsNullOrWhiteSpace(api.GroupName))
                {
                    return new[] { api.GroupName };
                }

                var controller = api.ActionDescriptor.RouteValues.TryGetValue("controller", out var name)
                    ? name
                    : "Default";
                return new[] { controller };
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static WebApplication UseSpotsterSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Spotster API v1");
            options.DocumentTitle = "Spotster API";
        });

        return app;
    }
}
