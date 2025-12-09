using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using PosApp.Web.Data;

namespace PosApp.Web.Features.Dashboard;

file sealed record TotalsRow(
    int TotalUsers,
    int ActiveUsers,
    int ActiveProducts,
    double? TodaySales,
    double? WeeklySales);

public sealed record DashboardSnapshot(
    int TotalUsers,
    int ActiveUsers,
    int ActiveProducts,
    decimal TodaySales,
    decimal WeeklySales,
    IReadOnlyList<TrendPoint> WeeklyTrend);

public sealed record TrendPoint(string Label, decimal Total);

public sealed class DashboardService
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DashboardService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var providerName = connection.GetType().Name;
        var isSqlite = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        var totalsSql = isSqlite
            ? @"SELECT
                    (SELECT COUNT(1) FROM Users) AS TotalUsers,
                    (SELECT COUNT(1) FROM Users WHERE IsActive = 1) AS ActiveUsers,
                    (SELECT COUNT(1) FROM Products WHERE IsActive = 1) AS ActiveProducts,
                    (SELECT IFNULL(SUM(GrandTotal), 0) FROM Sales WHERE date(CreatedAt) = date('now')) AS TodaySales,
                    (SELECT IFNULL(SUM(GrandTotal), 0) FROM Sales WHERE datetime(CreatedAt) >= datetime('now', '-6 days')) AS WeeklySales"
            : @"SELECT
                    (SELECT COUNT(1) FROM Users) AS TotalUsers,
                    (SELECT COUNT(1) FROM Users WHERE IsActive = 1) AS ActiveUsers,
                    (SELECT COUNT(1) FROM Products WHERE IsActive = 1) AS ActiveProducts,
                    (SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE CAST(CreatedAt AS date) = CAST(SYSUTCDATETIME() AS date)) AS TodaySales,
                    (SELECT ISNULL(SUM(GrandTotal), 0) FROM Sales WHERE CreatedAt >= DATEADD(day, -6, SYSUTCDATETIME())) AS WeeklySales";

        var totals = await connection.QuerySingleAsync<TotalsRow>(totalsSql);

        var trendSql = isSqlite
            ? @"SELECT strftime('%m/%d', CreatedAt) AS Label,
                         IFNULL(SUM(GrandTotal), 0) AS Total
                  FROM Sales
                  WHERE datetime(CreatedAt) >= datetime('now', '-6 days')
                  GROUP BY Label
                  ORDER BY MIN(CreatedAt);"
            : @"WITH SalesAgg AS (
                    SELECT FORMAT(CreatedAt, 'MM/dd') AS Label,
                           ISNULL(SUM(GrandTotal), 0) AS Total,
                           MIN(CreatedAt) AS FirstDate
                    FROM Sales
                    WHERE CreatedAt >= DATEADD(day, -6, SYSUTCDATETIME())
                    GROUP BY FORMAT(CreatedAt, 'MM/dd')
                )
                SELECT Label, Total
                FROM SalesAgg
                ORDER BY FirstDate;";

        var trend = (await connection.QueryAsync<TrendPoint>(trendSql)).ToList();

        return new DashboardSnapshot(
            TotalUsers: (int)totals.TotalUsers,
            ActiveUsers: (int)totals.ActiveUsers,
            ActiveProducts: (int)totals.ActiveProducts,
            TodaySales: (decimal)(totals.TodaySales ?? 0d),
            WeeklySales: (decimal)(totals.WeeklySales ?? 0d),
            WeeklyTrend: trend);
    }
}
