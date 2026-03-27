namespace Ayora.Application.DTOs.Auth;

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt
);

