using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;
using PosApp.Web.Security;

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
        const string sql = @"SELECT u.UserId AS Id,
                                    u.FullName,
                                    u.Email,
                                    u.MobileNumber,
                                    COALESCE(r.Name, 'Unassigned') AS RoleName,
                                    u.IsActive,
                                    u.CreatedOn
                             FROM Users u
                             LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
                             LEFT JOIN Roles r ON ur.RoleId = r.Id
                             ORDER BY u.CreatedOn DESC";

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
        const string sql = @"SELECT u.UserId AS Id,
                                    u.FullName,
                                    u.Email,
                                    u.MobileNumber,
                                    ur.RoleId,
                                    u.IsActive
                             FROM Users u
                             LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
                             WHERE u.UserId = @Id";
        return await connection.QuerySingleOrDefaultAsync<UserDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string insertUserSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, IsActive)
                                       VALUES (@UserId, @FullName, @Email, @MobileNumber, 1);";
        const string insertAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                                       VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 0, 0);";
        const string insertRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                       VALUES (@UserId, @RoleId);";

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();
        var password = input.Password?.Trim();

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required when creating a user.");
        }

        var credentials = PasswordUtility.CreateHash(password);
        var userId = Guid.NewGuid().ToString();

        if (input.RoleId == Guid.Empty)
        {
            throw new InvalidOperationException("Role is required for a user.");
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(insertUserSql, new
        {
            UserId = userId,
            FullName = fullName,
            Email = email,
            MobileNumber = mobileNumber
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
        {
            AuthId = Guid.NewGuid().ToString(),
            UserId = userId,
            PasswordHash = credentials.Hash,
            PasswordSalt = credentials.Salt
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(insertRoleSql, new
        {
            UserId = userId,
            RoleId = input.RoleId.ToString()
        }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task UpdateAsync(Guid id, UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string updateUserSql = @"UPDATE Users
                                       SET FullName = @FullName,
                                           Email = @Email,
                                           MobileNumber = @MobileNumber,
                                           UpdatedOn = CURRENT_TIMESTAMP
                                       WHERE UserId = @UserId";
        const string deleteRolesSql = "DELETE FROM UserRoles WHERE UserId = @UserId";
        const string insertRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                       VALUES (@UserId, @RoleId)";

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();
        if (input.RoleId == Guid.Empty)
        {
            throw new InvalidOperationException("Role is required for a user.");
        }

        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(updateUserSql, new
        {
            UserId = id.ToString(),
            FullName = fullName,
            Email = email,
            MobileNumber = mobileNumber
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(deleteRolesSql, new
        {
            UserId = id.ToString()
        }, transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(insertRoleSql, new
        {
            UserId = id.ToString(),
            RoleId = input.RoleId.ToString()
        }, transaction, cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(input.Password))
        {
            var password = input.Password.Trim();
            var credentials = PasswordUtility.CreateHash(password);

            const string countSql = "SELECT COUNT(1) FROM UserAuth WHERE UserId = @UserId";
            var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, new
            {
                UserId = id.ToString()
            }, transaction, cancellationToken: cancellationToken));

            if (existing > 0)
            {
                const string updateAuthSql = @"UPDATE UserAuth
                                               SET PasswordHash = @PasswordHash,
                                                   PasswordSalt = @PasswordSalt,
                                                   UpdatedOn = CURRENT_TIMESTAMP
                                               WHERE UserId = @UserId";

                await connection.ExecuteAsync(new CommandDefinition(updateAuthSql, new
                {
                    UserId = id.ToString(),
                    PasswordHash = credentials.Hash,
                    PasswordSalt = credentials.Salt
                }, transaction, cancellationToken: cancellationToken));
            }
            else
            {
                const string insertAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                                               VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 0, 0)";

                await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
                {
                    AuthId = Guid.NewGuid().ToString(),
                    UserId = id.ToString(),
                    PasswordHash = credentials.Hash,
                    PasswordSalt = credentials.Salt
                }, transaction, cancellationToken: cancellationToken));
            }
        }

        transaction.Commit();
    }

    public async Task ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Users
                             SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END,
                                 UpdatedOn = CURRENT_TIMESTAMP
                             WHERE UserId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: cancellationToken));
    }
}
