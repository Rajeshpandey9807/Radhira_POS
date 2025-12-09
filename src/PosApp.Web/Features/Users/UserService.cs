using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Users;

public sealed class UserService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public UserService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT u.Id, u.Username, u.DisplayName, u.Email, u.RoleId, r.Name AS RoleName,
                                    u.IsActive, datetime(u.CreatedAt) AS CreatedAt
                             FROM Users u
                             INNER JOIN Roles r ON u.RoleId = r.Id
                             ORDER BY u.CreatedAt DESC";

        var result = await connection.QueryAsync<UserAccount>(sql);
        return result.ToList();
    }

    public async Task<IReadOnlyList<RoleOption>> GetRoleOptionsAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT Id, Name FROM Roles ORDER BY Name";
        var result = await connection.QueryAsync<RoleOption>(sql);
        return result.ToList();
    }

    public async Task<UserDetails?> GetDetailsAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT Id, Username, DisplayName, Email, RoleId, IsActive FROM Users WHERE Id = @Id";
        return await connection.QuerySingleOrDefaultAsync<UserDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"INSERT INTO Users (Id, Username, DisplayName, Email, RoleId, PasswordHash, IsActive)
                             VALUES (@Id, @Username, @DisplayName, @Email, @RoleId, @PasswordHash, 1);";

        var normalizedUsername = input.Username.Trim().ToLowerInvariant();

        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid().ToString(),
            Username = normalizedUsername,
            DisplayName = displayName,
            Email = email,
            RoleId = input.RoleId.ToString(),
            PasswordHash = "changeme"
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Users
                             SET Username = @Username,
                                 DisplayName = @DisplayName,
                                 Email = @Email,
                                 RoleId = @RoleId,
                                 UpdatedAt = CURRENT_TIMESTAMP
                             WHERE Id = @Id";

        var normalizedUsername = input.Username.Trim().ToLowerInvariant();
        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            Username = normalizedUsername,
            DisplayName = displayName,
            Email = email,
            RoleId = input.RoleId.ToString()
        }, cancellationToken: cancellationToken));
    }

    public async Task ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Users
                             SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
                                 UpdatedAt = CURRENT_TIMESTAMP
                             WHERE Id = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));
    }
}
