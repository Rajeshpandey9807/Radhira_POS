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

        var legacyTableName = await EnsureUsersSchemaAsync(connection, cancellationToken);
        await EnsureUserAuthSchemaAsync(connection, cancellationToken);
        await EnsureUserRolesSchemaAsync(connection, cancellationToken);

        if (legacyTableName is not null)
        {
            await MigrateLegacyUserAuthAsync(connection, legacyTableName, cancellationToken);
            await MigrateLegacyUserRolesAsync(connection, legacyTableName, cancellationToken);
            await DropLegacyUsersTableAsync(connection, legacyTableName, cancellationToken);
        }

        await EnsureRegistrationTypesSchemaAsync(connection, cancellationToken);
        await SeedDefaultsAsync(connection, cancellationToken);
    }

    private static async Task<string?> EnsureUsersSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string createSql = @"CREATE TABLE IF NOT EXISTS Users (
                UserId TEXT PRIMARY KEY,
                FullName TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE,
                MobileNumber TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );";

        const string columnQuery = "SELECT name FROM pragma_table_info('Users');";
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnQuery, cancellationToken: cancellationToken))).ToList();

        if (columns.Count == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(createSql, cancellationToken: cancellationToken));
            return null;
        }

        var hasNewSchema = columns.Contains("UserId", StringComparer.OrdinalIgnoreCase);
        if (hasNewSchema)
        {
            return null;
        }

        const string renameSql = "ALTER TABLE Users RENAME TO Users_Legacy;";
        await connection.ExecuteAsync(new CommandDefinition(renameSql, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(createSql, cancellationToken: cancellationToken));

        const string migrateSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, IsActive, CreatedOn, UpdatedOn)
                                    SELECT Id,
                                           DisplayName,
                                           Email,
                                           PhoneNumber,
                                           IsActive,
                                           COALESCE(CreatedAt, CURRENT_TIMESTAMP),
                                           COALESCE(UpdatedAt, CURRENT_TIMESTAMP)
                                    FROM Users_Legacy;";
        await connection.ExecuteAsync(new CommandDefinition(migrateSql, cancellationToken: cancellationToken));

        return "Users_Legacy";
    }

    private static async Task EnsureUserAuthSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS UserAuth (
                AuthId TEXT PRIMARY KEY,
                UserId TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                LastLoginAt TEXT NULL,
                EmailVerified INTEGER NOT NULL DEFAULT 0,
                MobileVerified INTEGER NOT NULL DEFAULT 0,
                CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedOn TEXT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(UserId)
            );";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task EnsureUserRolesSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"CREATE TABLE IF NOT EXISTS UserRoles (
                UserId TEXT PRIMARY KEY,
                RoleId TEXT NOT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(UserId),
                FOREIGN KEY(RoleId) REFERENCES Roles(Id)
            );";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task MigrateLegacyUserAuthAsync(IDbConnection connection, string legacyTableName, CancellationToken cancellationToken)
    {
        var sql = $@"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
                     SELECT lower(hex(randomblob(16))) AS AuthId,
                            legacy.Id,
                            COALESCE(legacy.PasswordHash, ''),
                            '',
                            0,
                            0
                     FROM {legacyTableName} legacy
                     WHERE NOT EXISTS (SELECT 1 FROM UserAuth ua WHERE ua.UserId = legacy.Id);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task MigrateLegacyUserRolesAsync(IDbConnection connection, string legacyTableName, CancellationToken cancellationToken)
    {
        var sql = $@"INSERT INTO UserRoles (UserId, RoleId)
                     SELECT legacy.Id,
                            legacy.RoleId
                     FROM {legacyTableName} legacy
                     WHERE legacy.RoleId IS NOT NULL
                       AND NOT EXISTS (SELECT 1 FROM UserRoles ur WHERE ur.UserId = legacy.Id);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task DropLegacyUsersTableAsync(IDbConnection connection, string legacyTableName, CancellationToken cancellationToken)
    {
        var dropSql = $"DROP TABLE IF EXISTS {legacyTableName};";
        await connection.ExecuteAsync(new CommandDefinition(dropSql, cancellationToken: cancellationToken));
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

        var credentials = PasswordUtility.CreateHash("changeme");

        const string adminUserSql = @"INSERT INTO Users (UserId, FullName, Email, MobileNumber, IsActive)
            VALUES (@UserId, @FullName, @Email, @MobileNumber, 1)
            ON CONFLICT(Email) DO NOTHING;";

        var adminUserId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f").ToString();

        var adminInserted = await connection.ExecuteAsync(new CommandDefinition(adminUserSql, new
        {
            UserId = adminUserId,
            FullName = "Super Admin",
            Email = "admin@example.com",
            MobileNumber = "+1 555 0100"
        }, cancellationToken: cancellationToken));

        const string adminAuthSql = @"INSERT INTO UserAuth (AuthId, UserId, PasswordHash, PasswordSalt, EmailVerified, MobileVerified)
            VALUES (@AuthId, @UserId, @PasswordHash, @PasswordSalt, 0, 0)
            ON CONFLICT(UserId) DO NOTHING;";

        await connection.ExecuteAsync(new CommandDefinition(adminAuthSql, new
        {
            AuthId = Guid.NewGuid().ToString(),
            UserId = adminUserId,
            PasswordHash = credentials.Hash,
            PasswordSalt = credentials.Salt
        }, cancellationToken: cancellationToken));

        const string adminRoleSql = @"INSERT INTO UserRoles (UserId, RoleId)
            VALUES (@UserId, @RoleId)
            ON CONFLICT(UserId) DO NOTHING;";

        await connection.ExecuteAsync(new CommandDefinition(adminRoleSql, new
        {
            UserId = adminUserId,
            RoleId = adminRoleId.ToString()
        }, cancellationToken: cancellationToken));

        if (adminInserted > 0)
        {
            _logger.LogInformation("Seeded default admin credentials (admin / changeme). Please update immediately.");
        }
    }
}
