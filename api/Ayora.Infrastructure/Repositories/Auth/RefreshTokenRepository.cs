using Ayora.Domain.Entities.Auth;
using Ayora.Domain.Interfaces.Auth;
using Ayora.Infrastructure.Data.Abstractions;
using Dapper;

namespace Ayora.Infrastructure.Repositories.Auth;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct)
    {
        const string sql = """
                           SELECT TOP (1)
                               Id,
                               UserId,
                               Token,
                               ExpiryDate,
                               IsRevoked,
                               CreatedAt
                           FROM dbo.RefreshTokens
                           WHERE Token = @Token
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            new CommandDefinition(sql, new { Token = token }, cancellationToken: ct));
    }

    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO dbo.RefreshTokens (Id, UserId, Token, ExpiryDate, IsRevoked, CreatedAt)
                           VALUES (@Id, @UserId, @Token, @ExpiryDate, @IsRevoked, @CreatedAt)
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, refreshToken, cancellationToken: ct));
    }

    public async Task RevokeAsync(Guid refreshTokenId, CancellationToken ct)
    {
        const string sql = """
                           UPDATE dbo.RefreshTokens
                           SET IsRevoked = 1
                           WHERE Id = @Id
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = refreshTokenId }, cancellationToken: ct));
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
    {
        const string sql = """
                           UPDATE dbo.RefreshTokens
                           SET IsRevoked = 1
                           WHERE UserId = @UserId AND IsRevoked = 0
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
    }
}

