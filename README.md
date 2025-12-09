# Radhira POS

A pink-forward ASP.NET Core 8 MVC starter that wires up Dapper + SQLite for a lightweight point-of-sale (POS) experience. It ships with a dashboard, a stylized selling screen, and dynamic user management so you can invite, edit, or disable staff members without touching SQL.

## Features
- **Pink retail theme** built with custom CSS, animated hero cards, and responsive layouts.
- **Dashboard snapshot** backed by Dapper queries that surface user counts and rolling sales trends.
- **Dynamic user administration** (list, create, edit, toggle status) persisted in SQLite via repositories.
- **POS mock surface** to demonstrate the intended kiosk experience while business logic is wired up.
- **Automatic database bootstrapper** that creates all required tables and seeds starter roles + admin.

## Getting started
```bash
# Restore & run the MVC app
cd /workspace
DOTNET_ENVIRONMENT=Development dotnet run --project src/PosApp.Web/PosApp.Web.csproj
```
The first launch will create `posapp.db` in the project root and seed an `admin / changeme` operator. Update the password immediately in a real deployment.

## Suggested database tables
The bootstrapper provisions these tables; the structure doubles as a recommended schema for a production-ready POS. Customize columns as needed (e.g., add tenant IDs or audit fields).

| Table | Purpose | Key Columns |
| --- | --- | --- |
| `Roles` | Defines permission bundles for each operator type. | `Id (TEXT PK)`, `Name (UNIQUE)`, `Permissions` JSON/CSV |
| `Users` | Cashiers and admins that can authenticate. | `Id`, `Username (UNIQUE)`, `DisplayName`, `Email`, `RoleId (FK Roles)`, `PasswordHash`, `IsActive`, timestamps |
| `Categories` | Color-coded groupings for the catalog UI. | `Id`, `Name (UNIQUE)`, `Color` |
| `Products` | Sellable SKUs with pricing + reorder info. | `Id`, `Sku (UNIQUE)`, `Name`, `CategoryId`, `UnitPrice`, `ReorderPoint`, `IsActive` |
| `Customers` | Optional buyer profiles for loyalty or invoices. | `Id`, `DisplayName`, `Email`, `Phone`, `CreatedAt` |
| `Sales` | Header row per ticket/receipt. | `Id`, `ReceiptNumber (UNIQUE)`, `CustomerId`, `SubTotal`, `Tax`, `Discount`, `GrandTotal`, `Status`, `CreatedAt` |
| `SaleItems` | Line items that connect sales to products. | `Id`, `SaleId (FK Sales)`, `ProductId (FK Products)`, `Quantity`, `UnitPrice`, `LineTotal` |
| `Payments` | Tracks how each sale was settled. | `Id`, `SaleId`, `Amount`, `Method`, `Reference`, `PaidAt` |
| `StockMovements` | Audit trail for inventory adjustments. | `Id`, `ProductId`, `MovementType`, `Quantity`, `Reference`, `CreatedAt` |

> Extend with `Discounts`, `Taxes`, `InventoryBatches`, or `Stores` tables if you need multi-location support.

## Next steps
- Plug the POS screen into live product/sale services.
- Replace the placeholder password scheme with ASP.NET Identity or your auth provider of choice.
- Add unit/integration tests for repositories and controllers.
