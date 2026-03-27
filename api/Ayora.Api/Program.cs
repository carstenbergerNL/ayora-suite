using System.Text;
using Ayora.Application;
using Ayora.Application.Options.Auth;
using Ayora.Infrastructure;
using Ayora.Shared;
using Ayora.Shared.Modularity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration);

    var hasConsoleSink = ctx.Configuration
        .GetSection("Serilog:WriteTo")
        .GetChildren()
        .Any(s => string.Equals(s["Name"], "Console", StringComparison.OrdinalIgnoreCase));

    if (!hasConsoleSink)
    {
        cfg.MinimumLevel.Override("Microsoft", LogEventLevel.Warning);
        cfg.MinimumLevel.Override("System", LogEventLevel.Warning);
        cfg.WriteTo.Console();
    }
});

builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Invalid value." : e.ErrorMessage).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        return new BadRequestObjectResult(Ayora.Shared.Contracts.Api.ApiResponse<object>.Fail(
            "validation.failed",
            "One or more validation errors occurred.",
            errors));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(Ayora.Api.Swagger.SwaggerConfig.Configure);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        if (origins.Length == 0)
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddShared();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddTransient<Ayora.Api.Middleware.ExceptionHandlingMiddleware>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RefreshTokenOptions>(builder.Configuration.GetSection(RefreshTokenOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"{JwtOptions.SectionName} configuration is missing.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authorization = context.Request.Headers.Authorization.ToString().Trim();
                if (string.IsNullOrWhiteSpace(authorization))
                {
                    return Task.CompletedTask;
                }

                const string bearerPrefix = "Bearer ";

                if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var token = authorization[bearerPrefix.Length..].Trim();

                    // Be tolerant for malformed values like: "Bearer Bearer <token>"
                    while (token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        token = token[bearerPrefix.Length..].Trim();
                    }

                    context.Token = token;
                    return Task.CompletedTask;
                }

                // Also accept raw token values for tools that only send the JWT string.
                context.Token = authorization;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var moduleAssemblies = Ayora.Api.Modularity.ModuleAssemblyLoader.LoadFromModulesFolder(builder.Environment.ContentRootPath);
builder.Services.RegisterModulesFrom(AppDomain.CurrentDomain.GetAssemblies().Concat(moduleAssemblies));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<Ayora.Api.Middleware.ExceptionHandlingMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors("DefaultCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDiscoveredModules();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
