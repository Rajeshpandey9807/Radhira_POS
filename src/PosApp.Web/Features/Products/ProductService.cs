using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Products;

public sealed class ProductService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProductTypeOption>> GetProductTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT ProductTypeId, ProductTypeName
                             FROM ProductTypes
                             WHERE IsActive = 1
                             ORDER BY ProductTypeName;";
        var result = await connection.QueryAsync<ProductTypeOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT CategoryId, CategoryName
                             FROM Categories
                             WHERE IsActive = 1
                             ORDER BY CategoryName;";
        var result = await connection.QueryAsync<CategoryOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<GstRateOption>> GetGstRatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT GstRateId, GstRateName, RatePercentage
                             FROM GstRates
                             WHERE IsActive = 1
                             ORDER BY RatePercentage;";
        var result = await connection.QueryAsync<GstRateOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<UnitOption>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT UnitId, UnitName, UnitSymbol
                             FROM Units
                             WHERE IsActive = 1
                             ORDER BY UnitName;";
        var result = await connection.QueryAsync<UnitOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
SELECT
    p.ProductId,
    p.ItemName,
    p.ItemCode,
    COALESCE(c.CategoryName, '') AS CategoryName,
    COALESCE(pt.ProductTypeName, '') AS ProductTypeName,
    pp.SalesPrice,
    ps.CurrentStock,
    COALESCE(u.UnitSymbol, u.UnitName) AS UnitSymbol,
    gr.RatePercentage AS GstRate,
    p.IsActive
FROM Products p
LEFT JOIN Categories c ON c.CategoryId = p.CategoryId
LEFT JOIN ProductTypes pt ON pt.ProductTypeId = p.ProductTypeId
LEFT JOIN ProductPricing pp ON pp.ProductId = p.ProductId
LEFT JOIN ProductStock ps ON ps.ProductId = p.ProductId
LEFT JOIN Units u ON u.UnitId = ps.UnitId
LEFT JOIN GstRates gr ON gr.GstRateId = pp.GstRateId
ORDER BY p.ItemName;";

        var result = await connection.QueryAsync<ProductListItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<ProductEditRequest?> GetProductForEditAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
SELECT
    p.ProductId,
    p.ProductTypeId,
    p.CategoryId,
    p.ItemName,
    p.ItemCode,
    p.HSNCode,
    p.Description,
    p.IsActive,
    pp.SalesPrice,
    pp.PurchasePrice,
    pp.MRP,
    pp.GstRateId,
    ps.OpeningStock,
    ps.CurrentStock,
    ps.UnitId,
    ps.AsOfDate
FROM Products p
LEFT JOIN ProductPricing pp ON pp.ProductId = p.ProductId
LEFT JOIN ProductStock ps ON ps.ProductId = p.ProductId
WHERE p.ProductId = @ProductId;";

        var product = await connection.QuerySingleOrDefaultAsync<ProductEditRequest>(
            new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: cancellationToken));

        return product;
    }

    public async Task<Guid> CreateAsync(ProductCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var productId = Guid.NewGuid();

            // Insert into Products table
            const string insertProductSql = @"
INSERT INTO Products (ProductId, ProductTypeId, CategoryId, ItemName, ItemCode, HSNCode, Description, IsActive)
VALUES (@ProductId, @ProductTypeId, @CategoryId, @ItemName, @ItemCode, @HSNCode, @Description, @IsActive);";

            await connection.ExecuteAsync(new CommandDefinition(insertProductSql, new
            {
                ProductId = productId,
                ProductTypeId = request.ProductTypeId,
                CategoryId = request.CategoryId,
                ItemName = request.ItemName.Trim(),
                ItemCode = request.ItemCode?.Trim(),
                HSNCode = request.HSNCode?.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Insert into ProductPricing table
            const string insertPricingSql = @"
INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId)
VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId);";

            await connection.ExecuteAsync(new CommandDefinition(insertPricingSql, new
            {
                ProductId = productId,
                SalesPrice = request.SalesPrice ?? 0,
                PurchasePrice = request.PurchasePrice,
                MRP = request.MRP,
                GstRateId = request.GstRateId
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Insert into ProductStock table
            const string insertStockSql = @"
INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate)
VALUES (@ProductId, @OpeningStock, @CurrentStock, @UnitId, @AsOfDate);";

            await connection.ExecuteAsync(new CommandDefinition(insertStockSql, new
            {
                ProductId = productId,
                OpeningStock = request.OpeningStock ?? 0,
                CurrentStock = request.CurrentStock ?? request.OpeningStock ?? 0,
                UnitId = request.UnitId,
                AsOfDate = request.AsOfDate ?? DateTime.Today
            }, transaction: transaction, cancellationToken: cancellationToken));

            transaction.Commit();
            return productId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(Guid productId, ProductCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // Update Products table
            const string updateProductSql = @"
UPDATE Products
SET ProductTypeId = @ProductTypeId,
    CategoryId = @CategoryId,
    ItemName = @ItemName,
    ItemCode = @ItemCode,
    HSNCode = @HSNCode,
    Description = @Description,
    IsActive = @IsActive
WHERE ProductId = @ProductId;";

            await connection.ExecuteAsync(new CommandDefinition(updateProductSql, new
            {
                ProductId = productId,
                ProductTypeId = request.ProductTypeId,
                CategoryId = request.CategoryId,
                ItemName = request.ItemName.Trim(),
                ItemCode = request.ItemCode?.Trim(),
                HSNCode = request.HSNCode?.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Update or Insert ProductPricing
            const string updatePricingSql = @"
IF EXISTS (SELECT 1 FROM ProductPricing WHERE ProductId = @ProductId)
    UPDATE ProductPricing
    SET SalesPrice = @SalesPrice,
        PurchasePrice = @PurchasePrice,
        MRP = @MRP,
        GstRateId = @GstRateId
    WHERE ProductId = @ProductId;
ELSE
    INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId)
    VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId);";

            await connection.ExecuteAsync(new CommandDefinition(updatePricingSql, new
            {
                ProductId = productId,
                SalesPrice = request.SalesPrice ?? 0,
                PurchasePrice = request.PurchasePrice,
                MRP = request.MRP,
                GstRateId = request.GstRateId
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Update or Insert ProductStock
            const string updateStockSql = @"
IF EXISTS (SELECT 1 FROM ProductStock WHERE ProductId = @ProductId)
    UPDATE ProductStock
    SET OpeningStock = @OpeningStock,
        CurrentStock = @CurrentStock,
        UnitId = @UnitId,
        AsOfDate = @AsOfDate
    WHERE ProductId = @ProductId;
ELSE
    INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate)
    VALUES (@ProductId, @OpeningStock, @CurrentStock, @UnitId, @AsOfDate);";

            await connection.ExecuteAsync(new CommandDefinition(updateStockSql, new
            {
                ProductId = productId,
                OpeningStock = request.OpeningStock ?? 0,
                CurrentStock = request.CurrentStock ?? request.OpeningStock ?? 0,
                UnitId = request.UnitId,
                AsOfDate = request.AsOfDate ?? DateTime.Today
            }, transaction: transaction, cancellationToken: cancellationToken));

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<string> GenerateItemCodeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        
        const string sql = @"
SELECT TOP 1 ItemCode
FROM Products
WHERE ItemCode IS NOT NULL AND ItemCode LIKE 'ITM-%'
ORDER BY ItemCode DESC;";

        var lastCode = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(lastCode))
        {
            return "ITM-0001";
        }

        // Extract number from ITM-XXXX format
        var parts = lastCode.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out var number))
        {
            return $"ITM-{(number + 1):D4}";
        }

        return "ITM-0001";
    }
}
