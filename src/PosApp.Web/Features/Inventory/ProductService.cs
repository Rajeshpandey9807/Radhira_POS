using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Inventory;

public sealed class ProductService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string sql = @"
SELECT
    p.ProductId,
    p.ItemName,
    p.ItemCode,
    COALESCE(pt.ProductTypeName, '') AS ProductTypeName,
    COALESCE(c.CategoryName, '') AS CategoryName,
    pr.SalesPrice,
    pr.GstRateId,
    st.CurrentStock,
    p.IsActive
FROM dbo.Products p
LEFT JOIN dbo.ProductTypes pt ON pt.ProductTypeId = p.ProductTypeId
LEFT JOIN dbo.Categories c ON c.CategoryId = p.CategoryId
OUTER APPLY (
    SELECT TOP 1 SalesPrice, GstRateId
    FROM dbo.ProductPricing
    WHERE ProductId = p.ProductId
    ORDER BY CreatedOn DESC, PricingId DESC
) pr
OUTER APPLY (
    SELECT TOP 1 CurrentStock
    FROM dbo.ProductStock
    WHERE ProductId = p.ProductId
    ORDER BY CreatedOn DESC, StockId DESC
) st
ORDER BY p.ProductId DESC;";

        var result = await connection.QueryAsync<ProductListItem>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return result.ToList();
    }

    public async Task<ProductCreatePageModel> GetCreatePageAsync(ProductCreateRequest? existing = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        const string productTypesSql = @"SELECT ProductTypeId AS Id, ProductTypeName AS Name
                                         FROM dbo.ProductTypes
                                         WHERE IsActive = 1
                                         ORDER BY ProductTypeName;";
        const string categoriesSql = @"SELECT CategoryId AS Id, CategoryName AS Name
                                       FROM dbo.Categories
                                       WHERE IsActive = 1
                                       ORDER BY CategoryName;";
        const string unitsSql = @"SELECT UnitId AS Id, UnitName AS Name
                                  FROM dbo.Units
                                  WHERE IsActive = 1
                                  ORDER BY UnitName;";

        var productTypesTask = connection.QueryAsync<LookupOption>(new CommandDefinition(productTypesSql, cancellationToken: cancellationToken));
        var categoriesTask = connection.QueryAsync<LookupOption>(new CommandDefinition(categoriesSql, cancellationToken: cancellationToken));
        var unitsTask = connection.QueryAsync<LookupOption>(new CommandDefinition(unitsSql, cancellationToken: cancellationToken));

        await Task.WhenAll(productTypesTask, categoriesTask, unitsTask);

        return new ProductCreatePageModel
        {
            Form = existing ?? new ProductCreateRequest(),
            ProductTypes = (await productTypesTask).ToList(),
            Categories = (await categoriesTask).ToList(),
            Units = (await unitsTask).ToList()
        };
    }

    public async Task<Guid> CreateAsync(ProductCreateRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        var productId = Guid.NewGuid();
        var openingStock = request.OpeningStock ?? 0m;
        var asOfDate = (request.AsOfDate?.Date ?? DateTime.UtcNow.Date);

        try
        {
            const string insertProductSql = @"
INSERT INTO dbo.Products (ProductId, ProductTypeId, CategoryId, ItemName, ItemCode, HSNCode, [Description], IsActive)
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

            const string insertPricingSql = @"
INSERT INTO dbo.ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId)
VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId);";

            await connection.ExecuteAsync(new CommandDefinition(insertPricingSql, new
            {
                ProductId = productId,
                SalesPrice = request.SalesPrice,
                PurchasePrice = request.PurchasePrice,
                MRP = request.MRP,
                GstRateId = request.GstRateId
            }, transaction: transaction, cancellationToken: cancellationToken));

            const string insertStockSql = @"
INSERT INTO dbo.ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate)
VALUES (@ProductId, @OpeningStock, @CurrentStock, @UnitId, @AsOfDate);";

            await connection.ExecuteAsync(new CommandDefinition(insertStockSql, new
            {
                ProductId = productId,
                OpeningStock = openingStock,
                CurrentStock = openingStock,
                UnitId = request.UnitId,
                AsOfDate = asOfDate
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
}

