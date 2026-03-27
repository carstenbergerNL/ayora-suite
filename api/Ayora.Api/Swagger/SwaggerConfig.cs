using Microsoft.OpenApi;

namespace Ayora.Api.Swagger;

public static class SwaggerConfig
{
    public static void Configure(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions c)
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Ayora API",
            Version = "v1"
        });

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Description = "Enter: Bearer {your JWT access token}"
        };

        c.AddSecurityDefinition("Bearer", scheme);
        var schemeRef = new OpenApiSecuritySchemeReference("Bearer", null, null);

        c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            [schemeRef] = new List<string>()
        });
    }
}

