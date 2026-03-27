using Ayora.Application.Interfaces.Auth;
using Ayora.Application.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Ayora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

