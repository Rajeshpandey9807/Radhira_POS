using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Roles;

public enum RoleDeleteResult
{
    Success,
    InUse,
    NotFound
}

public sealed class RoleService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RoleListItem>> GetAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT r.RoleId AS Id, r.Name, COALESCE(r.Permissions, '') AS Permissions,
                                    (SELECT COUNT(*) FROM UserRoles ur WHERE ur.RoleId = r.RoleId) AS AssignedUsers
                             FROM Roles r
                             ORDER BY r.Name";

        var result = await connection.QueryAsync<RoleListItem>(sql);
        return result.ToList();
    }

    public async Task<RoleDetails?> GetByIdAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT RoleId AS Id, Name, COALESCE(Permissions, '') AS Permissions
                             FROM Roles
                             WHERE RoleId = @Id";
        return await connection.QuerySingleOrDefaultAsync<RoleDetails>(sql, new { Id = id });
    }

    public async Task CreateAsync(RoleInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO Roles (Name, Permissions)
                             VALUES (@Name, @Permissions)";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Name = input.Name.Trim(),
            Permissions = input.Permissions.Trim()
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(int id, RoleInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Roles
                             SET Name = @Name,
                                 Permissions = @Permissions
                             WHERE RoleId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            Name = input.Name.Trim(),
            Permissions = input.Permissions.Trim()
        }, cancellationToken: cancellationToken));
    }

    public async Task<RoleDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string usageSql = "SELECT COUNT(*) FROM UserRoles WHERE RoleId = @Id";
        var usageCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(usageSql, new { Id = id }, cancellationToken: cancellationToken));

        if (usageCount > 0)
        {
            return RoleDeleteResult.InUse;
        }

        const string deleteSql = "DELETE FROM Roles WHERE RoleId = @Id";
        var affected = await connection.ExecuteAsync(new CommandDefinition(deleteSql, new { Id = id }, cancellationToken: cancellationToken));

        return affected > 0 ? RoleDeleteResult.Success : RoleDeleteResult.NotFound;
    }
}
