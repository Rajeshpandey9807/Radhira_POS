using System.Data;
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
        const string sql = @"
            SELECT ProductTypeId, ProductTypeName
            FROM ProductTypes
            WHERE IsActive = 1
            ORDER BY ProductTypeName";
        return (await connection.QueryAsync<ProductTypeOption>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<CategoryOption>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT CategoryId, CategoryName
            FROM Categories
            WHERE IsActive = 1
            ORDER BY CategoryName";
        return (await connection.QueryAsync<CategoryOption>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<UnitOption>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT UnitId, UnitName, UnitCode
            FROM Units
            WHERE IsActive = 1
            ORDER BY UnitName";
        return (await connection.QueryAsync<UnitOption>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<GstRateOption>> GetGstRatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT GstRateId, Rate, Description
            FROM GstRates
            WHERE IsActive = 1
            ORDER BY Rate";
        return (await connection.QueryAsync<GstRateOption>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<Guid> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var productId = Guid.NewGuid();

            // Insert into Products table
            const string insertProductSql = @"
                INSERT INTO Products (ProductId, ProductTypeId, CategoryId, ItemName, ItemCode, HSNCode, Description, IsActive)
                VALUES (@ProductId, @ProductTypeId, @CategoryId, @ItemName, @ItemCode, @HSNCode, @Description, @IsActive)";

            await connection.ExecuteAsync(new CommandDefinition(insertProductSql, new
            {
                ProductId = productId,
                request.ProductTypeId,
                request.CategoryId,
                request.ItemName,
                request.ItemCode,
                request.HSNCode,
                request.Description,
                IsActive = request.IsActive
            }, transaction: transaction, cancellationToken: cancellationToken));

            // Insert into ProductPricing table if pricing details are provided
            if (request.SalesPrice.HasValue || request.PurchasePrice.HasValue || request.MRP.HasValue || request.GstRateId.HasValue)
            {
                const string insertPricingSql = @"
                    INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId)
                    VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId)";

                await connection.ExecuteAsync(new CommandDefinition(insertPricingSql, new
                {
                    ProductId = productId,
                    request.SalesPrice,
                    request.PurchasePrice,
                    request.MRP,
                    request.GstRateId
                }, transaction: transaction, cancellationToken: cancellationToken));
            }

            // Insert into ProductStock table if stock details are provided
            if (request.OpeningStock.HasValue || request.CurrentStock.HasValue || request.UnitId.HasValue || request.AsOfDate.HasValue)
            {
                const string insertStockSql = @"
                    INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate)
                    VALUES (@ProductId, @OpeningStock, @CurrentStock, @UnitId, @AsOfDate)";

                await connection.ExecuteAsync(new CommandDefinition(insertStockSql, new
                {
                    ProductId = productId,
                    request.OpeningStock,
                    CurrentStock = request.CurrentStock ?? request.OpeningStock,
                    request.UnitId,
                    request.AsOfDate
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

    public async Task<IReadOnlyList<ProductViewModel>> GetAllProductsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT 
                p.ProductId,
                p.ProductTypeId,
                pt.ProductTypeName,
                p.CategoryId,
                c.CategoryName,
                p.ItemName,
                p.ItemCode,
                p.HSNCode,
                p.Description,
                p.IsActive,
                pp.SalesPrice,
                pp.PurchasePrice,
                pp.MRP,
                pp.GstRateId,
                gr.Rate,
                ps.OpeningStock,
                ps.CurrentStock,
                ps.UnitId,
                u.UnitName,
                ps.AsOfDate,
                p.CreatedOn
            FROM Products p
            LEFT JOIN ProductTypes pt ON pt.ProductTypeId = p.ProductTypeId
            LEFT JOIN Categories c ON c.CategoryId = p.CategoryId
            LEFT JOIN ProductPricing pp ON pp.ProductId = p.ProductId
            LEFT JOIN GstRates gr ON gr.GstRateId = pp.GstRateId
            LEFT JOIN ProductStock ps ON ps.ProductId = p.ProductId
            LEFT JOIN Units u ON u.UnitId = ps.UnitId
            ORDER BY p.CreatedOn DESC";

        return (await connection.QueryAsync<ProductViewModel>(
            new CommandDefinition(sql, cancellationToken: cancellationToken))).ToList();
    }
}
