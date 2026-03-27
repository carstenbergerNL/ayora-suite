using Ayora.Domain.Entities.Auth;
using Ayora.Domain.Interfaces.Auth;
using Ayora.Infrastructure.Data.Abstractions;
using Dapper;

namespace Ayora.Infrastructure.Repositories.Auth;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        const string sql = """
                           SELECT TOP (1)
                               Id,
                               Email,
                               PasswordHash,
                               IsActive,
                               CreatedAt
                           FROM dbo.Users
                           WHERE Id = @Id
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        const string sql = """
                           SELECT TOP (1)
                               Id,
                               Email,
                               PasswordHash,
                               IsActive,
                               CreatedAt
                           FROM dbo.Users
                           WHERE Email = @Email
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<User>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: ct));
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct)
    {
        const string sql = """
                           SELECT CAST(CASE WHEN EXISTS (
                               SELECT 1 FROM dbo.Users WHERE Email = @Email
                           ) THEN 1 ELSE 0 END AS bit)
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Email = email }, cancellationToken: ct));
    }

    public async Task CreateAsync(User user, CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO dbo.Users (Id, Email, PasswordHash, IsActive, CreatedAt)
                           VALUES (@Id, @Email, @PasswordHash, @IsActive, @CreatedAt)
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: ct));
    }

    public async Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken ct)
    {
        const string sql = """
                           UPDATE dbo.Users
                           SET PasswordHash = @PasswordHash
                           WHERE Id = @UserId
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: ct));
    }
}

