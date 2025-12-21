using Microsoft.AspNetCore.Mvc;
using PosApp.Web.Data;
using PosApp.Web.Models;
using Dapper;

namespace PosApp.Web.Controllers;

public class ItemsController : Controller
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ItemsController(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IActionResult Inventory()
    {
        ViewData["Title"] = "Inventory";
        return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Title"] = "Add New Product";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        using var connection = await _connectionFactory.CreateConnectionAsync();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Resolve IDs
            // Category
            var categoryId = await connection.QueryFirstOrDefaultAsync<Guid?>(
                "SELECT Id FROM Categories WHERE Name = @Name", new { Name = model.Category }, transaction);

            if (categoryId == null)
            {
                // Create Category if not exists? Or just fail?
                // For "dynamic", let's create it.
                categoryId = Guid.NewGuid();
                await connection.ExecuteAsync(
                    "INSERT INTO Categories (Id, Name) VALUES (@Id, @Name)",
                    new { Id = categoryId, Name = model.Category }, transaction);
            }

            // Unit
            var unitName = !string.IsNullOrWhiteSpace(model.UnitAdvanced) ? model.UnitAdvanced : model.Unit;
            var unitId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT UnitId FROM Units WHERE UnitName = @Name", new { Name = unitName }, transaction);
            
            if (unitId == null)
            {
                 await connection.ExecuteAsync(
                     "INSERT INTO Units (UnitName) VALUES (@Name)", 
                     new { Name = unitName }, transaction);
                 unitId = await connection.QuerySingleAsync<int>(
                     "SELECT UnitId FROM Units WHERE UnitName = @Name", 
                     new { Name = unitName }, transaction);
            }

            // GST Rate
            var gstRateVal = model.GstRateAdvanced ?? model.GstRate;
            var gstRateId = await connection.QueryFirstOrDefaultAsync<int?>(
                "SELECT GstRateId FROM GstRates WHERE Rate = @Rate", new { Rate = gstRateVal }, transaction);

            if (gstRateId == null)
            {
                await connection.ExecuteAsync(
                     "INSERT INTO GstRates (Rate) VALUES (@Rate)", 
                     new { Rate = gstRateVal }, transaction);
                 gstRateId = await connection.QuerySingleAsync<int>(
                     "SELECT GstRateId FROM GstRates WHERE Rate = @Rate", 
                     new { Rate = gstRateVal }, transaction);
            }

            // Product Type (1=Goods, 2=Service)
            var productTypeId = model.ProductType == "Service" ? 2 : 1;

            // 2. Insert Product
            var productId = Guid.NewGuid();
            var itemCode = !string.IsNullOrWhiteSpace(model.ItemCode) ? model.ItemCode : null; 
            // Auto-generate code if needed?
            
            await connection.ExecuteAsync(@"
                INSERT INTO Products (ProductId, ProductTypeId, CategoryId, ItemName, ItemCode, HSNCode, Description, IsActive)
                VALUES (@ProductId, @ProductTypeId, @CategoryId, @ItemName, @ItemCode, @HSNCode, @Description, 1)",
                new {
                    ProductId = productId,
                    ProductTypeId = productTypeId,
                    CategoryId = categoryId,
                    model.ItemName,
                    ItemCode = itemCode,
                    model.HSNCode,
                    model.Description
                }, transaction);

            // 3. Insert Pricing
            await connection.ExecuteAsync(@"
                INSERT INTO ProductPricing (ProductId, SalesPrice, PurchasePrice, MRP, GstRateId)
                VALUES (@ProductId, @SalesPrice, @PurchasePrice, @MRP, @GstRateId)",
                new {
                    ProductId = productId,
                    SalesPrice = model.SalesPriceAdvanced ?? model.SalesPrice,
                    PurchasePrice = model.PurchasePrice,
                    MRP = model.Mrp,
                    GstRateId = gstRateId
                }, transaction);

            // 4. Insert Stock
            await connection.ExecuteAsync(@"
                INSERT INTO ProductStock (ProductId, OpeningStock, CurrentStock, UnitId, AsOfDate)
                VALUES (@ProductId, @OpeningStock, @CurrentStock, @UnitId, @AsOfDate)",
                new {
                    ProductId = productId,
                    OpeningStock = model.OpeningStock ?? 0,
                    CurrentStock = model.OpeningStock ?? 0,
                    UnitId = unitId,
                    AsOfDate = model.AsOfDate ?? DateTime.Now
                }, transaction);

            transaction.Commit();
            return RedirectToAction(nameof(Inventory));
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            ModelState.AddModelError("", "Error saving product: " + ex.Message);
            return View(model);
        }
    }

    public IActionResult Warehouse()
    {
        ViewData["Title"] = "Godown (Warehouse)";
        return View();
    }
}
