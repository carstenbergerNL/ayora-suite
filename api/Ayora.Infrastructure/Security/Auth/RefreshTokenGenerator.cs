using System.Security.Cryptography;
using Ayora.Application.Interfaces.Auth;

namespace Ayora.Infrastructure.Security.Auth;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public string Generate()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

