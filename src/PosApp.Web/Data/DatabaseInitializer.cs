using System;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Extensions.Logging;
using PosApp.Web.Security;

namespace PosApp.Web.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseInitializer> _logger;

    private const string UsersTableDefinition = @"(
        UserId TEXT PRIMARY KEY,
        FullName TEXT NOT NULL,
        Email TEXT NOT NULL UNIQUE,
        MobileNumber TEXT NOT NULL,
        RoleId TEXT NOT NULL,
        IsActive INTEGER NOT NULL DEFAULT 1,
        CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
        UpdatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
        FOREIGN KEY(RoleId) REFERENCES Roles(Id)
    );";

    private const string CreateUsersTableSql = "CREATE TABLE IF NOT EXISTS Users " + UsersTableDefinition;
    private const string CreateUsersTableSqlNoCheck = "CREATE TABLE Users " + UsersTableDefinition;

    private const string UserAuthTableDefinition = @"(
        AuthId TEXT PRIMARY KEY,
        UserId TEXT NOT NULL UNIQUE,
        PasswordHash TEXT NOT NULL,
        PasswordSalt TEXT NOT NULL,
        LastLoginAt TEXT NULL,
        EmailVerified INTEGER NOT NULL DEFAULT 0,
        MobileVerified INTEGER NOT NULL DEFAULT 0,
        FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE
    );";

    private const string CreateUserAuthTableSql = "CREATE TABLE IF NOT EXISTS UserAuth " + UserAuthTableDefinition;
    private const string UsersLegacyTableName = "Users_Legacy";

    private const string MigrateLegacyUsersSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, RoleId, IsActive, CreatedOn, UpdatedOn)
        SELECT Id,
               COALESCE(DisplayName, Username),
               Email,
               COALESCE(PhoneNumber, ''),
               RoleId,
               IsActive,
               COALESCE(CreatedAt, CURRENT_TIMESTAMP),
               COALESCE(UpdatedAt, CURRENT_TIMESTAMP)
        FROM Users_Legacy;";

    public DatabaseInitializer(IDbConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var providerName = connection.GetType().Name;
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        if (!isSqlite)
        {
            _logger.LogInformation("Skipping automatic SQLite schema setup for provider {ProviderName}. Ensure the SQL Server schema exists.", providerName);
            return;
        }

        var sqlCommands = new[]
        {
            "CREATE TABLE IF NOT EXISTS Roles (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Permissions TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS Categories (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Color TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS Products (Id TEXT PRIMARY KEY, Sku TEXT NOT NULL UNIQUE, Name TEXT NOT NULL, CategoryId TEXT NULL, UnitPrice REAL NOT NULL, ReorderPoint INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(CategoryId) REFERENCES Categories(Id));",
            "CREATE TABLE IF NOT EXISTS Customers (Id TEXT PRIMARY KEY, DisplayName TEXT NOT NULL, Email TEXT NULL, Phone TEXT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "CREATE TABLE IF NOT EXISTS Sales (Id TEXT PRIMARY KEY, ReceiptNumber TEXT NOT NULL UNIQUE, CustomerId TEXT NULL, SubTotal REAL NOT NULL, Tax REAL NOT NULL, Discount REAL NOT NULL, GrandTotal REAL NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(CustomerId) REFERENCES Customers(Id));",
            "CREATE TABLE IF NOT EXISTS SaleItems (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL, ProductId TEXT NOT NULL, Quantity INTEGER NOT NULL, UnitPrice REAL NOT NULL, LineTotal REAL NOT NULL, FOREIGN KEY(SaleId) REFERENCES Sales(Id), FOREIGN KEY(ProductId) REFERENCES Products(Id));",
            "CREATE TABLE IF NOT EXISTS Payments (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL, Amount REAL NOT NULL, Method TEXT NOT NULL, Reference TEXT NULL, PaidAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(SaleId) REFERENCES Sales(Id));",
            "CREATE TABLE IF NOT EXISTS StockMovements (Id TEXT PRIMARY KEY, ProductId TEXT NOT NULL, MovementType TEXT NOT NULL, Quantity INTEGER NOT NULL, Reference TEXT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(ProductId) REFERENCES Products(Id));",
            "CREATE TABLE IF NOT EXISTS BusinessTypes (BusinessTypeId INTEGER PRIMARY KEY AUTOINCREMENT, BusinessTypeName TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy INTEGER NOT NULL, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS RegistrationTypes (RegistrationTypeId INTEGER PRIMARY KEY AUTOINCREMENT, RegistrationTypeName TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy INTEGER NOT NULL, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS IndustryTypes (IndustryTypeId INTEGER PRIMARY KEY AUTOINCREMENT, IndustryTypeName TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy INTEGER NOT NULL, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS States (StateId INTEGER PRIMARY KEY AUTOINCREMENT, StateName TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy INTEGER NOT NULL, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL);"
        };

        foreach (var sql in sqlCommands)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
        }

        await EnsureUserSchemaAsync(connection, cancellationToken);
        await EnsureUserAuthSchemaAsync(connection, cancellationToken);
        await EnsureRegistrationTypesSchemaAsync(connection, cancellationToken);
        await SeedDefaultsAsync(connection, cancellationToken);
    }

    private static async Task EnsureUserSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var usersExists = await TableExistsAsync(connection, "Users", cancellationToken);
        var legacyExists = await TableExistsAsync(connection, UsersLegacyTableName, cancellationToken);

        if (!usersExists && !legacyExists)
        {
            await connection.ExecuteAsync(new CommandDefinition(CreateUsersTableSql, cancellationToken: cancellationToken));
            return;
        }

        if (!usersExists && legacyExists)
        {
            await connection.ExecuteAsync(new CommandDefinition(CreateUsersTableSqlNoCheck, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(MigrateLegacyUsersSql, cancellationToken: cancellationToken));
            return;
        }

        const string columnQuery = "SELECT name FROM pragma_table_info('Users');";
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnQuery, cancellationToken: cancellationToken))).ToList();
        var hasNewSchema = columns.Contains("UserId", StringComparer.OrdinalIgnoreCase)
            && columns.Contains("FullName", StringComparer.OrdinalIgnoreCase)
            && columns.Contains("MobileNumber", StringComparer.OrdinalIgnoreCase);

        if (hasNewSchema)
        {
            return;
        }

        if (legacyExists)
        {
            await connection.ExecuteAsync(new CommandDefinition($"DROP TABLE {UsersLegacyTableName};", cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition($"ALTER TABLE Users RENAME TO {UsersLegacyTableName};", cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(CreateUsersTableSqlNoCheck, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(MigrateLegacyUsersSql, cancellationToken: cancellationToken));
    }

    private static async Task EnsureUserAuthSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(CreateUserAuthTableSql, cancellationToken: cancellationToken));

        var legacyExists = await TableExistsAsync(connection, UsersLegacyTableName, cancellationToken);
        if (!legacyExists)
        {
            return;
        }

        const string legacyColumnQuery = $"SELECT name FROM pragma_table_info('{UsersLegacyTableName}');";
        var legacyColumns = (await connection.QueryAsync<string>(new CommandDefinition(legacyColumnQuery, cancellationToken: cancellationToken))).ToList();
        if (!legacyColumns.Contains("PasswordHash", StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition($"DROP TABLE {UsersLegacyTableName};", cancellationToken: cancellationToken));
            return;
        }

        const string migrateAuthSql = $@"
            INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
            SELECT lower(hex(randomblob(16))),
                   Id,
                   PasswordHash,
                   '' AS PasswordSalt,
                   0,
                   0
            FROM {UsersLegacyTableName} legacy
            WHERE PasswordHash IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM UserAuth auth WHERE auth.UserId = legacy.Id);";

        await connection.ExecuteAsync(new CommandDefinition(migrateAuthSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition($"DROP TABLE {UsersLegacyTableName};", cancellationToken: cancellationToken));
    }

    private static async Task<bool> TableExistsAsync(IDbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @Table;";
        var result = await connection.ExecuteScalarAsync<string>(new CommandDefinition(sql, new { Table = tableName }, cancellationToken: cancellationToken));
        return result is not null;
    }

    private static async Task EnsureRegistrationTypesSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string columnQuery = "SELECT name FROM pragma_table_info('RegistrationTypes');";
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnQuery, cancellationToken: cancellationToken))).ToList();

        if (columns.Count == 0)
        {
            return;
        }

        var hasNewSchema = columns.Contains("RegistrationTypeId", StringComparer.OrdinalIgnoreCase);
        if (hasNewSchema)
        {
            return;
        }

        const string renameSql = "ALTER TABLE RegistrationTypes RENAME TO RegistrationTypes_Legacy;";
        const string createSql = @"CREATE TABLE RegistrationTypes (
                RegistrationTypeId INTEGER PRIMARY KEY AUTOINCREMENT,
                RegistrationTypeName TEXT NOT NULL UNIQUE,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedBy INTEGER NOT NULL,
                CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedBy INTEGER NULL,
                UpdatedOn TEXT NULL
            );";
        const string migrateSql = @"INSERT INTO RegistrationTypes (RegistrationTypeName, IsActive, CreatedBy, CreatedOn, UpdatedBy, UpdatedOn)
            SELECT RegistrationTypeName,
                   IsActive,
                   0 AS CreatedBy,
                   CURRENT_TIMESTAMP AS CreatedOn,
                   NULL AS UpdatedBy,
                   NULL AS UpdatedOn
            FROM RegistrationTypes_Legacy;";
        const string dropSql = "DROP TABLE RegistrationTypes_Legacy;";

        var statements = new[] { renameSql, createSql, migrateSql, dropSql };
        foreach (var statement in statements)
        {
            await connection.ExecuteAsync(new CommandDefinition(statement, cancellationToken: cancellationToken));
        }
    }

    private async Task SeedDefaultsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        var adminRoleId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d001");
        var cashierRoleId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d002");

        const string insertRoleSql = @"INSERT INTO Roles (Id, Name, Permissions) VALUES (@Id, @Name, @Permissions)
            ON CONFLICT(Name) DO UPDATE SET Permissions = excluded.Permissions;";

        await connection.ExecuteAsync(new CommandDefinition(insertRoleSql, new
        {
            Id = adminRoleId.ToString(),
            Name = "Administrator",
            Permissions = "users:full,inventory:full,sales:full"
        }, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(insertRoleSql, new
        {
            Id = cashierRoleId.ToString(),
            Name = "Cashier",
            Permissions = "sales:read,sales:create"
        }, cancellationToken: cancellationToken));

        const string insertCategorySql = @"INSERT INTO Categories (Id, Name, Color) VALUES (@Id, @Name, @Color)
            ON CONFLICT(Name) DO NOTHING;";

        var defaultCategories = new[]
        {
            new { Id = Guid.Parse("3d2d9b93-5a7d-4b65-8b9d-6d438baaca11"), Name = "Coffee", Color = "#f06292" },
            new { Id = Guid.Parse("3d2d9b93-5a7d-4b65-8b9d-6d438baaca12"), Name = "Bakery", Color = "#f48fb1" },
            new { Id = Guid.Parse("3d2d9b93-5a7d-4b65-8b9d-6d438baaca13"), Name = "Retail", Color = "#f8bbd0" }
        };

        foreach (var category in defaultCategories)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertCategorySql, new
            {
                Id = category.Id.ToString(),
                category.Name,
                category.Color
            }, cancellationToken: cancellationToken));
        }

        const string adminUserSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, RoleId, IsActive, CreatedOn, UpdatedOn)
            VALUES (@UserId, @FullName, @Email, @MobileNumber, @RoleId, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT(Email) DO NOTHING;";

        var adminUserId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f");
        var adminPassword = PasswordUtility.CreatePasswordHash("changeme");

        var adminInserted = await connection.ExecuteAsync(new CommandDefinition(adminUserSql, new
        {
            UserId = adminUserId.ToString(),
            FullName = "Super Admin",
            Email = "admin@example.com",
            MobileNumber = "+1 555 0100",
            RoleId = adminRoleId.ToString()
        }, cancellationToken: cancellationToken));

        if (adminInserted > 0)
        {
            const string adminAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 1, 1)
                ON CONFLICT(UserId) DO UPDATE SET PasswordHash = excluded.PasswordHash, PasswordSalt = excluded.PasswordSalt;";

            await connection.ExecuteAsync(new CommandDefinition(adminAuthSql, new
            {
                AuthId = Guid.NewGuid().ToString(),
                UserId = adminUserId.ToString(),
                PasswordHash = adminPassword.Hash,
                PasswordSalt = adminPassword.Salt
            }, cancellationToken: cancellationToken));

            _logger.LogInformation("Seeded default admin credentials (email: {Email}, password: {Password}). Please update immediately.", "admin@example.com", "changeme");
        }
    }
}
