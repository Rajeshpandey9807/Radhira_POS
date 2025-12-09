using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;

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

        var sqlCommands = new[]
        {
            "CREATE TABLE IF NOT EXISTS Roles (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Permissions TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Username TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, Email TEXT NOT NULL, RoleId TEXT NOT NULL, PasswordHash TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(RoleId) REFERENCES Roles(Id));",
            "CREATE TABLE IF NOT EXISTS Categories (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Color TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS Products (Id TEXT PRIMARY KEY, Sku TEXT NOT NULL UNIQUE, Name TEXT NOT NULL, CategoryId TEXT NULL, UnitPrice REAL NOT NULL, ReorderPoint INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(CategoryId) REFERENCES Categories(Id));",
            "CREATE TABLE IF NOT EXISTS Customers (Id TEXT PRIMARY KEY, DisplayName TEXT NOT NULL, Email TEXT NULL, Phone TEXT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);",
            "CREATE TABLE IF NOT EXISTS Sales (Id TEXT PRIMARY KEY, ReceiptNumber TEXT NOT NULL UNIQUE, CustomerId TEXT NULL, SubTotal REAL NOT NULL, Tax REAL NOT NULL, Discount REAL NOT NULL, GrandTotal REAL NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(CustomerId) REFERENCES Customers(Id));",
            "CREATE TABLE IF NOT EXISTS SaleItems (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL, ProductId TEXT NOT NULL, Quantity INTEGER NOT NULL, UnitPrice REAL NOT NULL, LineTotal REAL NOT NULL, FOREIGN KEY(SaleId) REFERENCES Sales(Id), FOREIGN KEY(ProductId) REFERENCES Products(Id));",
            "CREATE TABLE IF NOT EXISTS Payments (Id TEXT PRIMARY KEY, SaleId TEXT NOT NULL, Amount REAL NOT NULL, Method TEXT NOT NULL, Reference TEXT NULL, PaidAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(SaleId) REFERENCES Sales(Id));",
            "CREATE TABLE IF NOT EXISTS StockMovements (Id TEXT PRIMARY KEY, ProductId TEXT NOT NULL, MovementType TEXT NOT NULL, Quantity INTEGER NOT NULL, Reference TEXT NULL, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, FOREIGN KEY(ProductId) REFERENCES Products(Id));"
        };

        foreach (var sql in sqlCommands)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
        }

        await SeedDefaultsAsync(connection, cancellationToken);
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

        const string adminUserSql = @"INSERT INTO Users (Id, Username, DisplayName, Email, RoleId, PasswordHash, IsActive)
            VALUES (@Id, @Username, @DisplayName, @Email, @RoleId, @PasswordHash, 1)
            ON CONFLICT(Username) DO NOTHING;";

        var adminInserted = await connection.ExecuteAsync(new CommandDefinition(adminUserSql, new
        {
            Id = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f").ToString(),
            Username = "admin",
            DisplayName = "Super Admin",
            Email = "admin@example.com",
            RoleId = adminRoleId.ToString(),
            PasswordHash = "changeme"
        }, cancellationToken: cancellationToken));

        if (adminInserted > 0)
        {
            _logger.LogInformation("Seeded default admin credentials (admin / changeme). Please update immediately.");
        }
    }
}
