using Ayora.Application.DTOs.Auth;
using Ayora.Application.Interfaces.Auth;
using Ayora.Application.Options.Auth;
using Ayora.Domain.Entities.Auth;
using Ayora.Domain.Interfaces.Auth;
using Ayora.Shared.Abstractions.Time;
using Ayora.Shared.Errors;
using BCrypt.Net;
using Microsoft.Extensions.Options;

namespace Ayora.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private const string DefaultRoleName = "User";

    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly ISystemClock _clock;
    private readonly RefreshTokenOptions _refreshTokenOptions;

    public AuthService(
        IUserRepository users,
        IRoleRepository roles,
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        IRefreshTokenGenerator refreshTokenGenerator,
        ISystemClock clock,
        IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _users = users;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _refreshTokenGenerator = refreshTokenGenerator;
        _clock = clock;
        _refreshTokenOptions = refreshTokenOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await _users.EmailExistsAsync(email, ct))
        {
            throw new AppException("auth.email_already_exists", "Email is already registered.", 409);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };

        await _users.CreateAsync(user, ct);

        var defaultRole = await _roles.GetByNameAsync(DefaultRoleName, ct);
        if (defaultRole is null)
        {
            throw new AppException("auth.role_seed_missing", "Default role is not configured.", 500);
        }

        await _roles.AssignRoleAsync(user.Id, defaultRole.Id, ct);

        var roles = await _roles.GetRolesForUserAsync(user.Id, ct);
        return await IssueTokensAsync(user, roles.Select(r => r.Name).ToArray(), ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, ct);
        if (user is null || !user.IsActive)
        {
            throw new AppException("auth.invalid_credentials", "Invalid credentials.", 401);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException("auth.invalid_credentials", "Invalid credentials.", 401);
        }

        var roles = await _roles.GetRolesForUserAsync(user.Id, ct);
        return await IssueTokensAsync(user, roles.Select(r => r.Name).ToArray(), ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var token = await _refreshTokens.GetByTokenAsync(refreshToken, ct);
        if (token is null)
        {
            return;
        }

        await _refreshTokens.RevokeAsync(token.Id, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var existing = await _refreshTokens.GetByTokenAsync(request.RefreshToken, ct);
        if (existing is null || existing.IsRevoked || existing.ExpiryDate <= _clock.UtcNow)
        {
            throw new AppException("auth.refresh_invalid", "Refresh token is invalid.", 401);
        }

        var user = await _users.GetByIdAsync(existing.UserId, ct);
        if (user is null || !user.IsActive)
        {
            throw new AppException("auth.refresh_invalid", "Refresh token is invalid.", 401);
        }

        await _refreshTokens.RevokeAsync(existing.Id, ct);

        var roles = await _roles.GetRolesForUserAsync(user.Id, ct);
        return await IssueTokensAsync(user, roles.Select(r => r.Name).ToArray(), ct);
    }

    public async Task<(Guid UserId, string Email, IReadOnlyList<string> Roles)> MeAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            throw new AppException("auth.user_not_found", "User not found.", 404);
        }

        var roles = await _roles.GetRolesForUserAsync(user.Id, ct);
        return (user.Id, user.Email, roles.Select(r => r.Name).ToArray());
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        if (request.CurrentPassword == request.NewPassword)
        {
            throw new AppException("auth.password_same", "New password must be different.", 400);
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            throw new AppException("auth.user_not_found", "User not found.", 404);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new AppException("auth.invalid_credentials", "Invalid credentials.", 401);
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _users.UpdatePasswordHashAsync(user.Id, newHash, ct);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, ct);

        if (user is null || !user.IsActive)
        {
            return;
        }

        _ = _tokenService.CreatePasswordResetToken(user.Id, user.Email);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (!_tokenService.TryValidatePasswordResetToken(request.ResetToken, out var userId, out var email))
        {
            throw new AppException("auth.reset_token_invalid", "Reset token is invalid.", 400);
        }

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive || !string.Equals(user.Email, NormalizeEmail(email), StringComparison.Ordinal))
        {
            throw new AppException("auth.reset_token_invalid", "Reset token is invalid.", 400);
        }

        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _users.UpdatePasswordHashAsync(user.Id, newHash, ct);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, IReadOnlyList<string> roleNames, CancellationToken ct)
    {
        var (accessToken, accessExpiresAt) = _tokenService.CreateAccessToken(user.Id, user.Email, roleNames);

        var refresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = _refreshTokenGenerator.Generate(),
            ExpiryDate = _clock.UtcNow.AddDays(_refreshTokenOptions.DaysToLive),
            IsRevoked = false,
            CreatedAt = _clock.UtcNow
        };

        await _refreshTokens.CreateAsync(refresh, ct);

        return new AuthResponse(
            user.Id,
            user.Email,
            roleNames,
            accessToken,
            accessExpiresAt,
            refresh.Token,
            refresh.ExpiryDate);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}

