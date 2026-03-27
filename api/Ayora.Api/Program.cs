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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

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
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDiscoveredModules();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();
