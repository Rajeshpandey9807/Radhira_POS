using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
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
        const string sql = @"SELECT u.Id, u.Username, u.DisplayName, u.Email, u.PhoneNumber,
                                    ur.RoleId, r.Name AS RoleName,
                                    u.IsActive, u.CreatedAt AS CreatedAt
                             FROM Users u
                             INNER JOIN UserRoles ur ON ur.UserId = u.Id
                             INNER JOIN Roles r ON ur.RoleId = r.Id
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
        const string sql = @"SELECT u.Id, u.Username, u.DisplayName, u.Email, u.PhoneNumber, ur.RoleId, u.IsActive
                             FROM Users u
                             INNER JOIN UserRoles ur ON ur.UserId = u.Id
                             WHERE u.Id = @Id";
        return await connection.QuerySingleOrDefaultAsync<UserDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var normalizedUsername = input.Username.Trim().ToLowerInvariant();
        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();
        var phoneNumber = input.PhoneNumber.Trim();
        var password = input.Password ?? throw new InvalidOperationException("Password is required when creating a user.");
        var userId = Guid.NewGuid().ToString();
        var passwordHash = PasswordUtility.HashPassword(password);

        const string insertUserSql = @"INSERT INTO Users (Id, Username, DisplayName, Email, PhoneNumber, IsActive, CreatedBy)
                                      VALUES (@Id, @Username, @DisplayName, @Email, @PhoneNumber, 1, @CreatedBy);";

        const string insertUserRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                          VALUES (@UserId, @RoleId);";

        const string insertAuthSql = @"INSERT INTO UserAuth (UserId, HashedPassword, PasswordSalt)
                                      VALUES (@UserId, @HashedPassword, @PasswordSalt);";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(insertUserSql, new
            {
                Id = userId,
                Username = normalizedUsername,
                DisplayName = displayName,
                Email = email,
                PhoneNumber = phoneNumber,
                CreatedBy = "system"
            }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(insertUserRoleSql, new
            {
                UserId = userId,
                RoleId = input.RoleId.ToString()
            }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
            {
                UserId = userId,
                HashedPassword = passwordHash.HashedPassword,
                PasswordSalt = passwordHash.PasswordSalt
            }, transaction: transaction, cancellationToken: cancellationToken));

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(Guid id, UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var sql = @"UPDATE Users
                    SET Username = @Username,
                        DisplayName = @DisplayName,
                        Email = @Email,
                        PhoneNumber = @PhoneNumber,
                        UpdatedAt = CURRENT_TIMESTAMP";

        var normalizedUsername = input.Username.Trim().ToLowerInvariant();
        var displayName = input.DisplayName.Trim();
        var email = input.Email.Trim();
        var phoneNumber = input.PhoneNumber.Trim();

        var parameters = new DynamicParameters();
        parameters.Add("Id", id.ToString());
        parameters.Add("Username", normalizedUsername);
        parameters.Add("DisplayName", displayName);
        parameters.Add("Email", email);
        parameters.Add("PhoneNumber", phoneNumber);

        sql += " WHERE Id = @Id";

        const string updateRoleSql = @"UPDATE UserRoles
                                      SET RoleId = @RoleId
                                      WHERE UserId = @UserId;";

        const string insertRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                      VALUES (@UserId, @RoleId);";

        const string updateAuthSql = @"UPDATE UserAuth
                                      SET HashedPassword = @HashedPassword,
                                          PasswordSalt = @PasswordSalt
                                      WHERE UserId = @UserId;";

        const string insertAuthSql = @"INSERT INTO UserAuth (UserId, HashedPassword, PasswordSalt)
                                      VALUES (@UserId, @HashedPassword, @PasswordSalt);";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: transaction, cancellationToken: cancellationToken));

            var roleParams = new { UserId = id.ToString(), RoleId = input.RoleId.ToString() };
            var roleAffected = await connection.ExecuteAsync(new CommandDefinition(updateRoleSql, roleParams, transaction: transaction, cancellationToken: cancellationToken));
            if (roleAffected == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(insertRoleSql, roleParams, transaction: transaction, cancellationToken: cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                var passwordHash = PasswordUtility.HashPassword(input.Password);
                var authParams = new
                {
                    UserId = id.ToString(),
                    HashedPassword = passwordHash.HashedPassword,
                    PasswordSalt = passwordHash.PasswordSalt
                };

                var authAffected = await connection.ExecuteAsync(new CommandDefinition(updateAuthSql, authParams, transaction: transaction, cancellationToken: cancellationToken));
                if (authAffected == 0)
                {
                    await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, authParams, transaction: transaction, cancellationToken: cancellationToken));
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
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
