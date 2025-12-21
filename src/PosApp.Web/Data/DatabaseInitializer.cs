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
            _logger.LogInformation("SQLite schema setup skipped for provider {ProviderName}. Ensuring required SQL Server tables exist.", providerName);
            await EnsureSqlServerSchemaAsync(connection, cancellationToken);
            return;
        }

        var sqlCommands = new[]
        {
            "CREATE TABLE IF NOT EXISTS RoleMaster (Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Permissions TEXT NULL);",
            // Normalized user schema:
            // - Users holds profile fields only (no auth, no role FK).
            // - UserRoles maps a user to a role.
            // - UserAuth stores salted password hashes.
            "CREATE TABLE IF NOT EXISTS Users (Id TEXT PRIMARY KEY, Username TEXT NOT NULL UNIQUE, DisplayName TEXT NOT NULL, Email TEXT NOT NULL, PhoneNumber TEXT NOT NULL, IsActive INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, CreatedBy TEXT NULL, UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy TEXT NULL);",
            "CREATE TABLE IF NOT EXISTS UserRoles (UserId TEXT PRIMARY KEY, RoleId TEXT NOT NULL, FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE, FOREIGN KEY(RoleId) REFERENCES RoleMaster(Id));",
            "CREATE TABLE IF NOT EXISTS UserAuth (UserId TEXT PRIMARY KEY, PasswordHash TEXT NOT NULL, PasswordSalt TEXT NOT NULL, FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE);",
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
            "CREATE TABLE IF NOT EXISTS States (StateId INTEGER PRIMARY KEY AUTOINCREMENT, StateName TEXT NOT NULL UNIQUE, IsActive INTEGER NOT NULL DEFAULT 1, CreatedBy INTEGER NOT NULL, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL);",

            // Business profile schema (SQLite/dev)
            "CREATE TABLE IF NOT EXISTS Businesses (BusinessId INTEGER PRIMARY KEY AUTOINCREMENT, BusinessName TEXT NOT NULL, CompanyPhoneNumber TEXT NULL, CompanyEmail TEXT NULL, IsGstRegistered INTEGER NOT NULL DEFAULT 0, GstNumber TEXT NULL, PanNumber TEXT NULL, IndustryTypeId INTEGER NULL, RegistrationTypeId INTEGER NULL, MsmeNumber TEXT NULL, Website TEXT NULL, AdditionalInfo TEXT NULL, LogoFileName TEXT NULL, LogoContentType TEXT NULL, LogoData BLOB NULL, SignatureFileName TEXT NULL, SignatureContentType TEXT NULL, SignatureData BLOB NULL, CreatedBy INTEGER NOT NULL DEFAULT 0, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL, FOREIGN KEY(IndustryTypeId) REFERENCES IndustryTypes(IndustryTypeId), FOREIGN KEY(RegistrationTypeId) REFERENCES RegistrationTypes(RegistrationTypeId));",
            "CREATE TABLE IF NOT EXISTS BusinessAddresses (BusinessAddressId INTEGER PRIMARY KEY AUTOINCREMENT, BusinessId INTEGER NOT NULL, BillingAddress TEXT NULL, City TEXT NULL, Pincode TEXT NULL, StateId INTEGER NULL, CreatedBy INTEGER NOT NULL DEFAULT 0, CreatedOn TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, UpdatedBy INTEGER NULL, UpdatedOn TEXT NULL, FOREIGN KEY(BusinessId) REFERENCES Businesses(BusinessId) ON DELETE CASCADE, FOREIGN KEY(StateId) REFERENCES States(StateId));",
            "CREATE TABLE IF NOT EXISTS BusinessBusinessTypes (BusinessId INTEGER NOT NULL, BusinessTypeId INTEGER NOT NULL, PRIMARY KEY (BusinessId, BusinessTypeId), FOREIGN KEY(BusinessId) REFERENCES Businesses(BusinessId) ON DELETE CASCADE, FOREIGN KEY(BusinessTypeId) REFERENCES BusinessTypes(BusinessTypeId));"
        };

        foreach (var sql in sqlCommands)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
        }

        await EnsureLegacyUserColumnsAsync(connection, cancellationToken);
        await EnsureNormalizedUserDataAsync(connection, cancellationToken);
        await EnsureRegistrationTypesSchemaAsync(connection, cancellationToken);
        await SeedDefaultsAsync(connection, cancellationToken);
    }

    private static async Task EnsureSqlServerSchemaAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        // SQL Server environments: ensure any required tables exist.
        // (This app uses Dapper + direct SQL rather than migrations.)
        var sql = @"
IF OBJECT_ID('dbo.Businesses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Businesses
    (
        BusinessId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BusinessName NVARCHAR(200) NOT NULL,
        CompanyPhoneNumber NVARCHAR(30) NULL,
        CompanyEmail NVARCHAR(200) NULL,
        IsGstRegistered BIT NOT NULL CONSTRAINT DF_Businesses_IsGstRegistered DEFAULT(0),
        GstNumber NVARCHAR(30) NULL,
        PanNumber NVARCHAR(20) NULL,
        IndustryTypeId INT NULL,
        RegistrationTypeId INT NULL,
        MsmeNumber NVARCHAR(50) NULL,
        Website NVARCHAR(200) NULL,
        AdditionalInfo NVARCHAR(800) NULL,
        LogoFileName NVARCHAR(260) NULL,
        LogoContentType NVARCHAR(100) NULL,
        LogoData VARBINARY(MAX) NULL,
        SignatureFileName NVARCHAR(260) NULL,
        SignatureContentType NVARCHAR(100) NULL,
        SignatureData VARBINARY(MAX) NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_Businesses_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Businesses_CreatedOn DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        UpdatedOn DATETIME2 NULL
    );

    IF OBJECT_ID('dbo.IndustryTypes', 'U') IS NOT NULL
        ALTER TABLE dbo.Businesses WITH CHECK ADD CONSTRAINT FK_Businesses_IndustryTypes
            FOREIGN KEY (IndustryTypeId) REFERENCES dbo.IndustryTypes(IndustryTypeId);

    IF OBJECT_ID('dbo.RegistrationTypes', 'U') IS NOT NULL
        ALTER TABLE dbo.Businesses WITH CHECK ADD CONSTRAINT FK_Businesses_RegistrationTypes
            FOREIGN KEY (RegistrationTypeId) REFERENCES dbo.RegistrationTypes(RegistrationTypeId);
END

IF OBJECT_ID('dbo.BusinessAddresses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessAddresses
    (
        BusinessAddressId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        BusinessId INT NOT NULL,
        BillingAddress NVARCHAR(500) NULL,
        City NVARCHAR(120) NULL,
        Pincode NVARCHAR(12) NULL,
        StateId INT NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_BusinessAddresses_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_BusinessAddresses_CreatedOn DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        UpdatedOn DATETIME2 NULL,
        CONSTRAINT FK_BusinessAddresses_Businesses FOREIGN KEY (BusinessId) REFERENCES dbo.Businesses(BusinessId) ON DELETE CASCADE
    );

    IF OBJECT_ID('dbo.States', 'U') IS NOT NULL
        ALTER TABLE dbo.BusinessAddresses WITH CHECK ADD CONSTRAINT FK_BusinessAddresses_States
            FOREIGN KEY (StateId) REFERENCES dbo.States(StateId);
END

IF OBJECT_ID('dbo.BusinessBusinessTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.BusinessBusinessTypes
    (
        BusinessId INT NOT NULL,
        BusinessTypeId INT NOT NULL,
        CONSTRAINT PK_BusinessBusinessTypes PRIMARY KEY (BusinessId, BusinessTypeId),
        CONSTRAINT FK_BusinessBusinessTypes_Businesses FOREIGN KEY (BusinessId) REFERENCES dbo.Businesses(BusinessId) ON DELETE CASCADE
    );

    IF OBJECT_ID('dbo.BusinessTypes', 'U') IS NOT NULL
        ALTER TABLE dbo.BusinessBusinessTypes WITH CHECK ADD CONSTRAINT FK_BusinessBusinessTypes_BusinessTypes
            FOREIGN KEY (BusinessTypeId) REFERENCES dbo.BusinessTypes(BusinessTypeId);
END

/* --------------------------
   Parties (billing) schema
   -------------------------- */
IF OBJECT_ID('dbo.PartyTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartyTypes
    (
        PartyTypeId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TypeName NVARCHAR(100) NOT NULL UNIQUE
    );
END

IF OBJECT_ID('dbo.PartyCategories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartyCategories
    (
        PartyCategoryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(120) NOT NULL UNIQUE
    );
END

IF OBJECT_ID('dbo.Parties', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Parties
    (
        PartyId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PartyName NVARCHAR(200) NOT NULL,
        MobileNumber NVARCHAR(30) NULL,
        Email NVARCHAR(200) NULL,
        OpeningBalance DECIMAL(18,2) NULL,
        GSTIN NVARCHAR(20) NULL,
        PANNumber NVARCHAR(20) NULL,
        PartyTypeId INT NOT NULL,
        PartyCategoryId INT NOT NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_Parties_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Parties_CreatedOn DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        UpdatedOn DATETIME2 NULL,
        CONSTRAINT FK_Parties_PartyTypes FOREIGN KEY (PartyTypeId) REFERENCES dbo.PartyTypes(PartyTypeId),
        CONSTRAINT FK_Parties_PartyCategories FOREIGN KEY (PartyCategoryId) REFERENCES dbo.PartyCategories(PartyCategoryId)
    );
END

IF COL_LENGTH('dbo.Parties', 'CreatedBy') IS NULL
    ALTER TABLE dbo.Parties ADD CreatedBy INT NOT NULL CONSTRAINT DF_Parties_CreatedBy_Alt DEFAULT(0);
IF COL_LENGTH('dbo.Parties', 'CreatedOn') IS NULL
    ALTER TABLE dbo.Parties ADD CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Parties_CreatedOn_Alt DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('dbo.Parties', 'UpdatedBy') IS NULL
    ALTER TABLE dbo.Parties ADD UpdatedBy INT NULL;
IF COL_LENGTH('dbo.Parties', 'UpdatedOn') IS NULL
    ALTER TABLE dbo.Parties ADD UpdatedOn DATETIME2 NULL;

IF OBJECT_ID('dbo.PartyAddresses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartyAddresses
    (
        PartyAddressId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PartyId INT NOT NULL,
        AddressType NVARCHAR(20) NOT NULL,
        Address NVARCHAR(500) NULL,
        CreditPeriod INT NULL,
        CreditLimit DECIMAL(18,2) NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_PartyAddresses_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyAddresses_CreatedOn DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PartyAddresses_Parties FOREIGN KEY (PartyId) REFERENCES dbo.Parties(PartyId) ON DELETE CASCADE
    );
    CREATE INDEX IX_PartyAddresses_PartyId ON dbo.PartyAddresses(PartyId);
END

IF COL_LENGTH('dbo.PartyAddresses', 'CreatedBy') IS NULL
    ALTER TABLE dbo.PartyAddresses ADD CreatedBy INT NOT NULL CONSTRAINT DF_PartyAddresses_CreatedBy_Alt DEFAULT(0);
IF COL_LENGTH('dbo.PartyAddresses', 'CreatedOn') IS NULL
    ALTER TABLE dbo.PartyAddresses ADD CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyAddresses_CreatedOn_Alt DEFAULT SYSUTCDATETIME();

IF OBJECT_ID('dbo.PartyContacts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartyContacts
    (
        ContactId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PartyId INT NOT NULL,
        ContactPersonName NVARCHAR(200) NULL,
        DateOfBirth DATE NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_PartyContacts_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyContacts_CreatedOn DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PartyContacts_Parties FOREIGN KEY (PartyId) REFERENCES dbo.Parties(PartyId) ON DELETE CASCADE
    );
    CREATE INDEX IX_PartyContacts_PartyId ON dbo.PartyContacts(PartyId);
END

IF COL_LENGTH('dbo.PartyContacts', 'CreatedBy') IS NULL
    ALTER TABLE dbo.PartyContacts ADD CreatedBy INT NOT NULL CONSTRAINT DF_PartyContacts_CreatedBy_Alt DEFAULT(0);
IF COL_LENGTH('dbo.PartyContacts', 'CreatedOn') IS NULL
    ALTER TABLE dbo.PartyContacts ADD CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyContacts_CreatedOn_Alt DEFAULT SYSUTCDATETIME();

IF OBJECT_ID('dbo.PartyBankDetails', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PartyBankDetails
    (
        BankDetailId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        PartyId INT NOT NULL,
        AccountNumber NVARCHAR(50) NULL,
        IFSC NVARCHAR(20) NULL,
        BranchName NVARCHAR(120) NULL,
        AccountHolderName NVARCHAR(200) NULL,
        UPI NVARCHAR(80) NULL,
        CreatedBy INT NOT NULL CONSTRAINT DF_PartyBankDetails_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyBankDetails_CreatedOn DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_PartyBankDetails_Parties FOREIGN KEY (PartyId) REFERENCES dbo.Parties(PartyId) ON DELETE CASCADE
    );
    CREATE INDEX IX_PartyBankDetails_PartyId ON dbo.PartyBankDetails(PartyId);
END

IF COL_LENGTH('dbo.PartyBankDetails', 'CreatedBy') IS NULL
    ALTER TABLE dbo.PartyBankDetails ADD CreatedBy INT NOT NULL CONSTRAINT DF_PartyBankDetails_CreatedBy_Alt DEFAULT(0);
IF COL_LENGTH('dbo.PartyBankDetails', 'CreatedOn') IS NULL
    ALTER TABLE dbo.PartyBankDetails ADD CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_PartyBankDetails_CreatedOn_Alt DEFAULT SYSUTCDATETIME();

/* Seed master data if empty */
IF NOT EXISTS (SELECT 1 FROM dbo.PartyTypes)
BEGIN
    IF COL_LENGTH('dbo.PartyTypes', 'TypeName') IS NOT NULL
        INSERT INTO dbo.PartyTypes (TypeName) VALUES (N'Customer'), (N'Vendor'), (N'Both');
END

IF NOT EXISTS (SELECT 1 FROM dbo.PartyCategories)
BEGIN
    IF COL_LENGTH('dbo.PartyCategories', 'CategoryName') IS NOT NULL
        INSERT INTO dbo.PartyCategories (CategoryName) VALUES (N'Retail'), (N'Wholesale'), (N'Distributor'), (N'Other');
END

/* --------------------------
   Product Types (lookup)
   -------------------------- */
IF OBJECT_ID('dbo.ProductTypes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductTypes
    (
        ProductTypeId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TypeName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive BIT NOT NULL CONSTRAINT DF_ProductTypes_IsActive DEFAULT(1),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_ProductTypes_CreatedOn DEFAULT SYSUTCDATETIME()
    );
    INSERT INTO dbo.ProductTypes (TypeName) VALUES (N'Goods'), (N'Service');
END

/* --------------------------
   Categories (for products) - Drop old schema if exists
   -------------------------- */
IF OBJECT_ID('dbo.Categories', 'U') IS NOT NULL AND COL_LENGTH('dbo.Categories', 'CategoryId') IS NULL
BEGIN
    -- Old schema detected (has Id instead of CategoryId), drop related tables first
    IF OBJECT_ID('dbo.SaleItems', 'U') IS NOT NULL
        DROP TABLE dbo.SaleItems;
    IF OBJECT_ID('dbo.StockMovements', 'U') IS NOT NULL
        DROP TABLE dbo.StockMovements;
    IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL
        DROP TABLE dbo.Products;
    DROP TABLE dbo.Categories;
END

IF OBJECT_ID('dbo.Categories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categories
    (
        CategoryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CategoryName NVARCHAR(100) NOT NULL UNIQUE,
        IsActive BIT NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT(1),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Categories_CreatedOn DEFAULT SYSUTCDATETIME()
    );
    INSERT INTO dbo.Categories (CategoryName) VALUES (N'Stationery'), (N'Beverages'), (N'Electronics'), (N'Services'), (N'Misc');
END

/* --------------------------
   Units (measuring units)
   -------------------------- */
IF OBJECT_ID('dbo.Units', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Units
    (
        UnitId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UnitName NVARCHAR(50) NOT NULL UNIQUE,
        IsActive BIT NOT NULL CONSTRAINT DF_Units_IsActive DEFAULT(1),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Units_CreatedOn DEFAULT SYSUTCDATETIME()
    );
    INSERT INTO dbo.Units (UnitName) VALUES (N'pcs'), (N'kg'), (N'liter'), (N'box'), (N'meter'), (N'pack');
END

/* --------------------------
   GST Rates
   -------------------------- */
IF OBJECT_ID('dbo.GstRates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.GstRates
    (
        GstRateId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RateName NVARCHAR(50) NOT NULL UNIQUE,
        Rate DECIMAL(5,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_GstRates_IsActive DEFAULT(1),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_GstRates_CreatedOn DEFAULT SYSUTCDATETIME()
    );
    INSERT INTO dbo.GstRates (RateName, Rate) VALUES (N'0%', 0), (N'5%', 5), (N'12%', 12), (N'18%', 18), (N'28%', 28);
END

/* --------------------------
   Products - Drop old schema if exists
   -------------------------- */
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL AND COL_LENGTH('dbo.Products', 'ProductId') IS NULL
BEGIN
    -- Old schema detected (has Id instead of ProductId), drop related tables first
    IF OBJECT_ID('dbo.ProductStock', 'U') IS NOT NULL
        DROP TABLE dbo.ProductStock;
    IF OBJECT_ID('dbo.ProductPricing', 'U') IS NOT NULL
        DROP TABLE dbo.ProductPricing;
    IF OBJECT_ID('dbo.SaleItems', 'U') IS NOT NULL
        DROP TABLE dbo.SaleItems;
    IF OBJECT_ID('dbo.StockMovements', 'U') IS NOT NULL
        DROP TABLE dbo.StockMovements;
    DROP TABLE dbo.Products;
END

IF OBJECT_ID('dbo.Products', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        ProductId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ProductTypeId INT NOT NULL,
        CategoryId INT NOT NULL,
        ItemName NVARCHAR(200) NOT NULL,
        ItemCode NVARCHAR(50) NULL,
        HSNCode NVARCHAR(20) NULL,
        Description NVARCHAR(500) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT(1),
        CreatedBy INT NOT NULL CONSTRAINT DF_Products_CreatedBy DEFAULT(0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedOn DEFAULT SYSUTCDATETIME(),
        UpdatedBy INT NULL,
        UpdatedOn DATETIME2 NULL,
        CONSTRAINT FK_Products_ProductTypes FOREIGN KEY (ProductTypeId) REFERENCES dbo.ProductTypes(ProductTypeId),
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
    );
    CREATE INDEX IX_Products_ItemCode ON dbo.Products(ItemCode);
    CREATE INDEX IX_Products_ItemName ON dbo.Products(ItemName);
END

/* --------------------------
   Product Pricing
   -------------------------- */
IF OBJECT_ID('dbo.ProductPricing', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductPricing
    (
        PricingId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        SalesPrice DECIMAL(18,2) NULL,
        PurchasePrice DECIMAL(18,2) NULL,
        MRP DECIMAL(18,2) NULL,
        GstRateId INT NULL,
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_ProductPricing_CreatedOn DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ProductPricing_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId) ON DELETE CASCADE,
        CONSTRAINT FK_ProductPricing_GstRates FOREIGN KEY (GstRateId) REFERENCES dbo.GstRates(GstRateId)
    );
    CREATE INDEX IX_ProductPricing_ProductId ON dbo.ProductPricing(ProductId);
END

/* --------------------------
   Product Stock
   -------------------------- */
IF OBJECT_ID('dbo.ProductStock', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductStock
    (
        StockId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        OpeningStock DECIMAL(18,2) NULL,
        CurrentStock DECIMAL(18,2) NULL,
        UnitId INT NULL,
        AsOfDate DATE NULL,
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_ProductStock_CreatedOn DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ProductStock_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId) ON DELETE CASCADE,
        CONSTRAINT FK_ProductStock_Units FOREIGN KEY (UnitId) REFERENCES dbo.Units(UnitId)
    );
    CREATE INDEX IX_ProductStock_ProductId ON dbo.ProductStock(ProductId);
END
";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task EnsureLegacyUserColumnsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string columnQuery = "SELECT name FROM pragma_table_info('Users');";
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnQuery, cancellationToken: cancellationToken))).ToList();

        if (!columns.Contains("PhoneNumber", StringComparer.OrdinalIgnoreCase))
        {
            const string addPhoneColumnSql = "ALTER TABLE Users ADD COLUMN PhoneNumber TEXT NOT NULL DEFAULT '';";
            await connection.ExecuteAsync(new CommandDefinition(addPhoneColumnSql, cancellationToken: cancellationToken));
        }
    }

    /// <summary>
    /// If the database was created with the legacy, denormalized schema (Users.RoleId / Users.PasswordHash),
    /// populate the new normalized tables so the current code path works.
    /// </summary>
    private static async Task EnsureNormalizedUserDataAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string columnQuery = "SELECT name FROM pragma_table_info('Users');";
        var columns = (await connection.QueryAsync<string>(new CommandDefinition(columnQuery, cancellationToken: cancellationToken))).ToList();

        var hasLegacyRoleId = columns.Contains("RoleId", StringComparer.OrdinalIgnoreCase);
        var hasLegacyPasswordHash = columns.Contains("PasswordHash", StringComparer.OrdinalIgnoreCase);

        if (!hasLegacyRoleId && !hasLegacyPasswordHash)
        {
            return;
        }

        if (hasLegacyRoleId)
        {
            const string migrateRolesSql = @"INSERT OR IGNORE INTO UserRoles (UserId, RoleId)
                                            SELECT Id AS UserId, RoleId
                                            FROM Users
                                            WHERE RoleId IS NOT NULL AND TRIM(RoleId) <> '';";
            await connection.ExecuteAsync(new CommandDefinition(migrateRolesSql, cancellationToken: cancellationToken));
        }

        if (hasLegacyPasswordHash)
        {
            // Legacy passwords were stored as an unsalted hash. We keep the value in UserAuth and leave salt empty.
            // (New/updated users will use salted PBKDF2 values.)
            const string migrateAuthSql = @"INSERT OR IGNORE INTO UserAuth (UserId, PasswordHash, PasswordSalt)
                                           SELECT Id AS UserId, PasswordHash AS PasswordHash, '' AS PasswordSalt
                                           FROM Users
                                           WHERE PasswordHash IS NOT NULL AND TRIM(PasswordHash) <> '';";
            await connection.ExecuteAsync(new CommandDefinition(migrateAuthSql, cancellationToken: cancellationToken));
        }
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

        const string insertRoleSql = @"INSERT INTO RoleMaster (Id, Name, Permissions) VALUES (@Id, @Name, @Permissions)
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

        const string adminUserSql = @"INSERT INTO Users (Id, Username, DisplayName, Email, PhoneNumber, IsActive)
            VALUES (@Id, @Username, @DisplayName, @Email, @PhoneNumber, 1)
            ON CONFLICT(Username) DO NOTHING;";

        var defaultAdminPassword = PasswordUtility.HashPassword("changeme");

        var adminInserted = await connection.ExecuteAsync(new CommandDefinition(adminUserSql, new
        {
            Id = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f").ToString(),
            Username = "admin",
            DisplayName = "Super Admin",
            Email = "admin@example.com",
            PhoneNumber = "+1 555 0100",
        }, cancellationToken: cancellationToken));

        // Ensure the admin role mapping + auth exist even if the user already existed.
        const string adminRoleMapSql = @"INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)
            ON CONFLICT(UserId) DO UPDATE SET RoleId = excluded.RoleId;";
        await connection.ExecuteAsync(new CommandDefinition(adminRoleMapSql, new
        {
            UserId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f").ToString(),
            RoleId = adminRoleId.ToString()
        }, cancellationToken: cancellationToken));

        const string adminAuthSql = @"INSERT INTO UserAuth (UserId, PasswordHash, PasswordSalt)
            VALUES (@UserId, @PasswordHash, @PasswordSalt)
            ON CONFLICT(UserId) DO UPDATE SET PasswordHash = excluded.PasswordHash, PasswordSalt = excluded.PasswordSalt;";
        await connection.ExecuteAsync(new CommandDefinition(adminAuthSql, new
        {
            UserId = Guid.Parse("0f340000-3df4-4ef7-8d3f-748f6ec9d00f").ToString(),
            PasswordHash = defaultAdminPassword.PasswordHash,
            PasswordSalt = defaultAdminPassword.PasswordSalt
        }, cancellationToken: cancellationToken));

        if (adminInserted > 0)
        {
            _logger.LogInformation("Seeded default admin credentials (admin / changeme). Please update immediately.");
        }

        // Seed business setup dropdown defaults (SQLite/dev only).
        // In SQL Server environments, schema + seed data are expected to be managed externally.
        const string insertBusinessTypeSql = @"INSERT OR IGNORE INTO BusinessTypes (BusinessTypeName, IsActive, CreatedBy)
                                              VALUES (@Name, 1, 0);";
        var defaultBusinessTypes = new[] { "Retail", "Wholesale", "Services", "Manufacturing" };
        foreach (var name in defaultBusinessTypes)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertBusinessTypeSql, new { Name = name }, cancellationToken: cancellationToken));
        }

        const string insertIndustryTypeSql = @"INSERT OR IGNORE INTO IndustryTypes (IndustryTypeName, IsActive, CreatedBy)
                                              VALUES (@Name, 1, 0);";
        var defaultIndustryTypes = new[] { "Grocery", "Restaurant", "Pharmacy", "Apparel", "Electronics" };
        foreach (var name in defaultIndustryTypes)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertIndustryTypeSql, new { Name = name }, cancellationToken: cancellationToken));
        }

        const string insertRegistrationTypeSql = @"INSERT OR IGNORE INTO RegistrationTypes (RegistrationTypeName, IsActive, CreatedBy)
                                                   VALUES (@Name, 1, 0);";
        var defaultRegistrationTypes = new[] { "Proprietorship", "Partnership", "LLP", "Private Limited", "Public Limited" };
        foreach (var name in defaultRegistrationTypes)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertRegistrationTypeSql, new { Name = name }, cancellationToken: cancellationToken));
        }

        const string insertStateSql = @"INSERT OR IGNORE INTO States (StateName, IsActive, CreatedBy)
                                        VALUES (@Name, 1, 0);";
        var defaultStates = new[] { "Tamil Nadu", "Karnataka", "Kerala", "Maharashtra", "Delhi" };
        foreach (var name in defaultStates)
        {
            await connection.ExecuteAsync(new CommandDefinition(insertStateSql, new { Name = name }, cancellationToken: cancellationToken));
        }
    }
}
