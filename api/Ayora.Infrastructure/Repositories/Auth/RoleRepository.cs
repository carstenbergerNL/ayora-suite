using Ayora.Domain.Entities.Auth;
using Ayora.Domain.Interfaces.Auth;
using Ayora.Infrastructure.Data.Abstractions;
using Dapper;

namespace Ayora.Infrastructure.Repositories.Auth;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Role?> GetByNameAsync(string name, CancellationToken ct)
    {
        const string sql = """
                           SELECT TOP (1)
                               Id,
                               Name
                           FROM dbo.Roles
                           WHERE Name = @Name
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Role>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Role>> GetRolesForUserAsync(Guid userId, CancellationToken ct)
    {
        const string sql = """
                           SELECT r.Id, r.Name
                           FROM dbo.Roles r
                           INNER JOIN dbo.UserRoles ur ON ur.RoleId = r.Id
                           WHERE ur.UserId = @UserId
                           ORDER BY r.Name
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        var roles = await conn.QueryAsync<Role>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct));
        return roles.ToArray();
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct)
    {
        const string sql = """
                           IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
                           BEGIN
                               INSERT INTO dbo.UserRoles (UserId, RoleId)
                               VALUES (@UserId, @RoleId)
                           END
                           """;

        using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { UserId = userId, RoleId = roleId }, cancellationToken: ct));
    }
}

