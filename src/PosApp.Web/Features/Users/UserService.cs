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
        const string sql = @"SELECT u.UserId, u.FullName, u.Email, u.MobileNumber,
                                    COALESCE(ur.RoleId, 0) AS RoleId,
                                    COALESCE(r.Name, '') AS RoleName,
                                    u.IsActive, u.CreatedOn
                             FROM Users u
                             LEFT JOIN UserRoles ur ON ur.UserId = u.UserId
                             LEFT JOIN RoleMaster r ON ur.RoleId = r.Id
                             ORDER BY u.CreatedOn DESC";

        var result = await connection.QueryAsync<UserAccount>(sql);
        return result.ToList();
    }

    public async Task<IReadOnlyList<RoleOption>> GetRoleOptionsAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT Id, Name FROM RoleMaster ORDER BY Name";
        var result = await connection.QueryAsync<RoleOption>(sql);
        return result.ToList();
    }

    public async Task<UserDetails?> GetDetailsAsync(int id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT u.UserId, u.FullName, u.Email, u.MobileNumber, ur.RoleId, u.IsActive
                             FROM Users u
                             INNER JOIN UserRoles ur ON ur.UserId = u.UserId
                             WHERE u.UserId = @UserId";
        return await connection.QuerySingleOrDefaultAsync<UserDetails>(sql, new { UserId = id });
    }

    public async Task CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();
        var password = input.Password ?? throw new InvalidOperationException("Password is required when creating a user.");
        var passwordHash = PasswordUtility.HashPassword(password);

        const string insertUserRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                          VALUES (@UserId, @RoleId);";

        const string insertAuthSql = @"INSERT INTO UserAuth (UserId, PasswordHash, PasswordSalt)
                                      VALUES (@UserId, @PasswordHash, @PasswordSalt);";

        try
        {
            // SQL Server (INT identity) requires getting the generated key. Using SCOPE_IDENTITY().
            // For SQLite (TEXT ids) this path is not used in production for this app; it is kept for dev/demo.
            const string insertUserAndReturnIdSql = @"INSERT INTO Users (FullName, Email, MobileNumber, IsActive, CreatedOn, CreatedBy)
                                                     VALUES (@FullName, @Email, @MobileNumber, 1, SYSUTCDATETIME(), @CreatedBy);
                                                     SELECT CAST(SCOPE_IDENTITY() AS int);";

            var userId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(insertUserAndReturnIdSql, new
            {
                FullName = fullName,
                Email = email,
                MobileNumber = mobileNumber,
                CreatedBy = 0
            }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(insertUserRoleSql, new
            {
                UserId = userId,
                RoleId = input.RoleId
            }, transaction: transaction, cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
            {
                UserId = userId,
                PasswordHash = passwordHash.PasswordHash,
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

    public async Task UpdateAsync(int id, UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var sql = @"UPDATE Users
                    SET FullName = @FullName,
                        Email = @Email,
                        MobileNumber = @MobileNumber";

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();

        var parameters = new DynamicParameters();
        parameters.Add("UserId", id);
        parameters.Add("FullName", fullName);
        parameters.Add("Email", email);
        parameters.Add("MobileNumber", mobileNumber);

        sql += " WHERE UserId = @UserId";

        const string updateRoleSql = @"UPDATE UserRoles
                                      SET RoleId = @RoleId
                                      WHERE UserId = @UserId;";

        const string insertRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
                                      VALUES (@UserId, @RoleId);";

        const string updateAuthSql = @"UPDATE UserAuth
                                      SET PasswordHash = @PasswordHash,
                                          PasswordSalt = @PasswordSalt
                                      WHERE UserId = @UserId;";

        const string insertAuthSql = @"INSERT INTO UserAuth (UserId, PasswordHash, PasswordSalt)
                                      VALUES (@UserId, @PasswordHash, @PasswordSalt);";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: transaction, cancellationToken: cancellationToken));

            var roleParams = new { UserId = id, RoleId = input.RoleId };
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
                    UserId = id,
                    PasswordHash = passwordHash.PasswordHash,
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

    public async Task<bool> SetStatusAsync(int id, bool activate, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"UPDATE Users
                             SET IsActive = @IsActive
                             WHERE UserId = @UserId";

        var affected = await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserId = id,
            IsActive = activate ? 1 : 0
        }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
