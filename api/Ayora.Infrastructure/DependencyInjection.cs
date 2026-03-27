using Ayora.Application.Interfaces.Auth;
using Ayora.Domain.Interfaces.Auth;
using Ayora.Infrastructure.Repositories.Auth;
using Ayora.Infrastructure.Security.Auth;
using Ayora.Infrastructure.Data.Abstractions;
using Ayora.Infrastructure.Data.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace Ayora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        return services;
    }
}

