using System.Collections.Generic;
using System.Data;
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
                                    u.RoleId,
                                    r.Name AS RoleName,
                                    u.IsActive,
                                    u.CreatedOn
                             FROM Users u
                             INNER JOIN Roles r ON u.RoleId = r.Id
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
        const string sql = @"SELECT UserId AS Id,
                                    FullName,
                                    Email,
                                    MobileNumber,
                                    RoleId,
                                    IsActive
                             FROM Users WHERE UserId = @Id";
        return await connection.QuerySingleOrDefaultAsync<UserDetails>(sql, new { Id = id.ToString() });
    }

    public async Task CreateAsync(UserInput input, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();
        var password = input.Password?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Password is required when creating a user.");
        }
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var passwordResult = PasswordUtility.CreatePasswordHash(password);

        const string insertUserSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, RoleId, IsActive, CreatedOn, UpdatedOn)
                                       VALUES (@UserId, @FullName, @Email, @MobileNumber, @RoleId, 1, @CreatedOn, @UpdatedOn);";

        const string insertAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                                       VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 0, 0);";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(insertUserSql, new
            {
                UserId = userId.ToString(),
                FullName = fullName,
                Email = email,
                MobileNumber = mobileNumber,
                RoleId = input.RoleId.ToString(),
                CreatedOn = now,
                UpdatedOn = now
            }, transaction, cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
            {
                AuthId = Guid.NewGuid().ToString(),
                UserId = userId.ToString(),
                PasswordHash = passwordResult.Hash,
                PasswordSalt = passwordResult.Salt
            }, transaction, cancellationToken));

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

        var fullName = input.FullName.Trim();
        var email = input.Email.Trim();
        var mobileNumber = input.MobileNumber.Trim();
        var now = DateTime.UtcNow;

        const string updateUserSql = @"UPDATE Users
                                       SET FullName = @FullName,
                                           Email = @Email,
                                           MobileNumber = @MobileNumber,
                                           RoleId = @RoleId,
                                           UpdatedOn = @UpdatedOn
                                       WHERE UserId = @UserId";

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(updateUserSql, new
            {
                UserId = id.ToString(),
                FullName = fullName,
                Email = email,
                MobileNumber = mobileNumber,
                RoleId = input.RoleId.ToString(),
                UpdatedOn = now
            }, transaction, cancellationToken));

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                var sanitizedPassword = input.Password!.Trim();
                var passwordResult = PasswordUtility.CreatePasswordHash(sanitizedPassword);
                const string updateAuthSql = @"UPDATE UserAuth
                                               SET PasswordHash = @PasswordHash,
                                                   PasswordSalt = @PasswordSalt
                                               WHERE UserId = @UserId";

                var affected = await connection.ExecuteAsync(new CommandDefinition(updateAuthSql, new
                {
                    UserId = id.ToString(),
                    PasswordHash = passwordResult.Hash,
                    PasswordSalt = passwordResult.Salt
                }, transaction, cancellationToken));

                if (affected == 0)
                {
                    const string insertAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                                                   VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 0, 0);";

                    await connection.ExecuteAsync(new CommandDefinition(insertAuthSql, new
                    {
                        AuthId = Guid.NewGuid().ToString(),
                        UserId = id.ToString(),
                        PasswordHash = passwordResult.Hash,
                        PasswordSalt = passwordResult.Salt
                    }, transaction, cancellationToken));
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
                                 UpdatedOn = @UpdatedOn
                             WHERE UserId = @Id";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id.ToString(),
            UpdatedOn = DateTime.UtcNow
        }, cancellationToken: cancellationToken));
    }
}
