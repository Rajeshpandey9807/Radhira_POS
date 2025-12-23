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

    /// <summary>
    /// Gets all active product types from the ProductTypes table.
    /// Returns ProductTypeId (value) and TypeName (display text) for dropdown binding.
    /// </summary>
    public async Task<IReadOnlyList<ProductTypeOption>> GetProductTypesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
SELECT 
    ProductTypeId,  -- Used as option value in dropdown
    TypeName        -- Used as option display text in dropdown
FROM dbo.ProductTypes
WHERE IsActive = 1
ORDER BY TypeName;";
        var result = await connection.QueryAsync<ProductTypeOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT CategoryId, CategoryName
                             FROM dbo.Categories
                             WHERE IsActive = 1
                             ORDER BY CategoryName;";
        var result = await connection.QueryAsync<CategoryOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<UnitOption>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT UnitId, UnitName
                             FROM dbo.Units
                             WHERE IsActive = 1
                             ORDER BY UnitName;";
        var result = await connection.QueryAsync<UnitOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<IReadOnlyList<GstRateOption>> GetGstRatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"SELECT GstRateId, RateName, Rate
                             FROM dbo.GstRates
                             WHERE IsActive = 1
                             ORDER BY Rate;";
        var result = await connection.QueryAsync<GstRateOption>(new CommandDefinition(sql, cancellationToken: cancellationToken));
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
    COALESCE(pt.TypeName, '') AS ProductTypeName,
    pp.SalesPrice,
    ps.CurrentStock,
    gr.Rate AS GstRate,
    p.IsActive
FROM Products p
LEFT JOIN Categories c ON c.CategoryId = p.CategoryId
LEFT JOIN ProductTypes pt ON pt.ProductTypeId = p.ProductTypeId
LEFT JOIN ProductPricing pp ON pp.ProductId = p.ProductId
LEFT JOIN ProductStock ps ON ps.ProductId = p.ProductId
LEFT JOIN GstRates gr ON gr.GstRateId = pp.GstRateId
ORDER BY p.ItemName;";

        var result = await connection.QueryAsync<ProductListItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<ProductEditRequest?> GetProductForEditAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string productSql = @"
SELECT TOP 1
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
    ps.UnitId,
    ps.AsOfDate
FROM Products p
LEFT JOIN ProductPricing pp ON pp.ProductId = p.ProductId
LEFT JOIN ProductStock ps ON ps.ProductId = p.ProductId
WHERE p.ProductId = @ProductId;";

        var product = await connection.QuerySingleOrDefaultAsync<ProductEditRequest>(
            new CommandDefinition(productSql, new { ProductId = productId }, cancellationToken: cancellationToken));

        return product;
    }

    public async Task<Guid> CreateAsync(ProductCreateRequest request, int createdBy = 0, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var productId = Guid.NewGuid();

            // Generate item code if not provided
            var itemCode = request.ItemCode?.Trim();
            if (string.IsNullOrWhiteSpace(itemCode))
            {
                itemCode = await GenerateItemCodeAsync(connection, transaction, cancellationToken);
            }

            const string insertProductSql = @"
INSERT INTO Products (ProductId, ProductTypeId, CategoryId, ItemName, ItemCode, HSNCode, Description, IsActive, CreatedBy, CreatedOn)
VALUES (@ProductId, @ProductTypeId, @CategoryId, @ItemName, @ItemCode, @HSNCode, @Description, @IsActive, @CreatedBy, GETDATE());";

            await connection.ExecuteAsync(new CommandDefinition(insertProductSql, new
            {
                ProductId = productId,
                ProductTypeId = request.ProductTypeId,
                CategoryId = request.CategoryId,
                ItemName = request.ItemName.Trim(),
                ItemCode = itemCode,
                HSNCode = request.HSNCode?.Trim(),
                Description = request.Description?.Trim(),
                IsActive = request.IsActive,
                CreatedBy = createdBy
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Insert Pricing Details
            if (request.SalesPrice.HasValue || request.PurchasePrice.HasValue || request.MRP.HasValue || request.GstRateId.HasValue)
            {
                const string insertPricingSql = @"
INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId, CreatedOn)
VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId, GETDATE());";

                await connection.ExecuteAsync(new CommandDefinition(insertPricingSql, new
                {
                    ProductId = productId,
                    SalesPrice = request.SalesPrice,
                    PurchasePrice = request.PurchasePrice,
                    MRP = request.MRP,
                    GstRateId = request.GstRateId
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            // Insert Stock Details
            if (request.OpeningStock.HasValue || request.UnitId.HasValue)
            {
                const string insertStockSql = @"
INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate, CreatedOn)
VALUES (@ProductId, @OpeningStock, @OpeningStock, @UnitId, @AsOfDate, GETDATE());";

                await connection.ExecuteAsync(new CommandDefinition(insertStockSql, new
                {
                    ProductId = productId,
                    OpeningStock = request.OpeningStock ?? 0,
                    UnitId = request.UnitId,
                    AsOfDate = request.AsOfDate ?? DateTime.Today
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
            return productId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(Guid productId, ProductCreateRequest request, int updatedBy = 0, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string updateProductSql = @"
UPDATE Products
SET ProductTypeId = @ProductTypeId,
    CategoryId = @CategoryId,
    ItemName = @ItemName,
    ItemCode = @ItemCode,
    HSNCode = @HSNCode,
    Description = @Description,
    IsActive = @IsActive,
    UpdatedBy = @UpdatedBy,
    UpdatedOn = GETDATE()
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
                IsActive = request.IsActive,
                UpdatedBy = updatedBy
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Delete and re-insert pricing
            const string deletePricingSql = "DELETE FROM ProductPricing WHERE ProductId = @ProductId;";
            await connection.ExecuteAsync(new CommandDefinition(deletePricingSql, new { ProductId = productId }, transaction: transaction, cancellationToken: cancellationToken));

            if (request.SalesPrice.HasValue || request.PurchasePrice.HasValue || request.MRP.HasValue || request.GstRateId.HasValue)
            {
                const string insertPricingSql = @"
INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId, CreatedOn)
VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId, GETDATE());";

                await connection.ExecuteAsync(new CommandDefinition(insertPricingSql, new
                {
                    ProductId = productId,
                    SalesPrice = request.SalesPrice,
                    PurchasePrice = request.PurchasePrice,
                    MRP = request.MRP,
                    GstRateId = request.GstRateId
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            // Delete and re-insert stock
            const string deleteStockSql = "DELETE FROM ProductStock WHERE ProductId = @ProductId;";
            await connection.ExecuteAsync(new CommandDefinition(deleteStockSql, new { ProductId = productId }, transaction: transaction, cancellationToken: cancellationToken));

            if (request.OpeningStock.HasValue || request.UnitId.HasValue)
            {
                const string insertStockSql = @"
INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate, CreatedOn)
VALUES (@ProductId, @OpeningStock, @OpeningStock, @UnitId, @AsOfDate, GETDATE());";

                await connection.ExecuteAsync(new CommandDefinition(insertStockSql, new
                {
                    ProductId = productId,
                    OpeningStock = request.OpeningStock ?? 0,
                    UnitId = request.UnitId,
                    AsOfDate = request.AsOfDate ?? DateTime.Today
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<string> GenerateBarcodeAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        
        // Generate a unique 8-digit barcode
        const int maxAttempts = 10;
        var random = new Random();
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // Generate random 8-digit number (10000000 to 99999999)
            var barcode = random.Next(10000000, 99999999).ToString();
            
            // Check if barcode already exists
            const string checkSql = @"
SELECT COUNT(1) 
FROM Products 
WHERE ItemCode = @Barcode;";
            
            var exists = await connection.QuerySingleAsync<int>(
                new CommandDefinition(checkSql, new { Barcode = barcode }, cancellationToken: cancellationToken));
            
            if (exists == 0)
            {
                return barcode;
            }
        }
        
        // If all attempts failed, use timestamp-based approach
        var timestampBarcode = (DateTime.UtcNow.Ticks % 90000000 + 10000000).ToString();
        return timestampBarcode;
    }

    private static async Task<string> GenerateItemCodeAsync(IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP 1 ItemCode 
FROM Products 
WHERE ItemCode LIKE 'ITM-%' 
ORDER BY ItemCode DESC;";

        var lastCode = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(lastCode))
        {
            return "ITM-0001";
        }

        var parts = lastCode.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out var num))
        {
            return $"ITM-{(num + 1):D4}";
        }

        return $"ITM-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
    }
}
