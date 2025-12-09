using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosApp.Web.Features.Dashboard;

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
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var today = DateTime.UtcNow.Date;
        var baseSales = 1250m;

        var trend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = today.AddDays(-offset);
                var total = baseSales + (6 - offset) * 75m;
                return (day, Point: new TrendPoint(day.ToString("MM/dd", CultureInfo.InvariantCulture), total));
            })
            .OrderBy(entry => entry.day)
            .Select(entry => entry.Point)
            .ToList();

        var snapshot = new DashboardSnapshot(
            TotalUsers: 18,
            ActiveUsers: 15,
            ActiveProducts: 42,
            TodaySales: trend.Last().Total,
            WeeklySales: trend.Sum(tp => tp.Total),
            WeeklyTrend: trend);

        return await Task.FromResult(snapshot);
    }
}
