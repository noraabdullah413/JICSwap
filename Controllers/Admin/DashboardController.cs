 
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Services;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class DashboardController : Controller
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    [HttpGet("/admin", Name = "admin.dashboard")]
    public IActionResult Index()
    {
        var now = Clock.Now();
        var month = Clock.StartOfMonth(now);
        var prevMonth = Clock.StartOfMonth(Clock.SubMonthsNoOverflow(now, 1));
        var ordersThis = Db.Orders.Count(o => o.OrderDate >= month);
        var ordersPrev = Db.Orders.Count(o => o.OrderDate >= prevMonth && o.OrderDate <= month);    
        var commissionThis = (double)(Db.Orders.Where(o => o.Status == "Completed" && o.CompletedDate >= month).Sum(o => (decimal?)o.CommissionFee) ?? 0m);
        var featuredThis = (double)(Db.Payments.Where(p => p.PaymentType == "Featured fee" && p.Status == "Paid" && p.PaymentDate >= month).Sum(p => (decimal?)p.Amount) ?? 0m);
        var weekAgo = now.AddDays(-7);

        var kpis = new List<Dictionary<string, object?>>
        {
            new() { ["label"] = "Active students", ["value"] = Helpers.NumberFormat(Db.Students.Count(s => s.AccountStatus == "Active")),
                ["foot"] = Db.Students.Count(s => s.JoinDate >= month) + " joined this month", ["icon"] = "bi-people", ["tone"] = "navy", ["url"] = Url.RouteUrl("admin.students.index") },
            new() { ["label"] = "Available listings", ["value"] = Helpers.NumberFormat(Db.Listings.Visible().Count()),
                ["foot"] = Db.Listings.Count(l => l.PostedDate >= weekAgo) + " posted this week", ["icon"] = "bi-shop", ["tone"] = "sky", ["url"] = Url.RouteUrl("admin.listings.index") },
            new() { ["label"] = "Orders this month", ["value"] = Helpers.NumberFormat(ordersThis),
                ["foot"] = Trend(ordersThis, ordersPrev) + " vs last month", ["icon"] = "bi-bag-check", ["tone"] = "green", ["url"] = Url.RouteUrl("admin.purchases.index") },
            new() { ["label"] = "Revenue this month", ["value"] = Helpers.Sar(commissionThis + featuredThis, 2),
                ["foot"] = Helpers.Sar(commissionThis, 2) + " commission · " + Helpers.Sar(featuredThis, 2) + " featured", ["icon"] = "bi-cash-stack", ["tone"] = "gold", ["url"] = Url.RouteUrl("admin.reports") },
        };

         
        var tasks = new List<object?[]>
        {
            new object?[] { "Services waiting for the proof check", Db.Listings.Count(l => l.ListingType == "Service" && l.ProofVerifiedAt == null && l.Status == "Available"),
                "bi-patch-question", "background:var(--purple-050);color:#4B2A9B;", Url.RouteUrl("admin.listings.index", new { tab = "proof" }) },
            new object?[] { "Open complaints", Db.Complaints.Count(c => c.Status == "Pending" || c.Status == "Under Review"),
                "bi-flag", "background:var(--error-050);color:var(--error);", Url.RouteUrl("admin.complaints.index") },
            new object?[] { "Return requests to decide", Db.ReturnRequests.Count(r => r.Status == "Pending"),
                "bi-arrow-counterclockwise", "background:var(--amber-050);color:var(--amber);", Url.RouteUrl("admin.returns.index") },
            new object?[] { "Requests waiting for a seller", Db.Orders.Count(o => o.Status == "Pending"),
                "bi-hourglass-split", "background:var(--sky-050);color:#0B5E86;", Url.RouteUrl("admin.purchases.index", new { status = "Pending" }) },
        };

         
        var from = Clock.Today().AddDays(-29);
        var daily = Db.Orders.Where(o => o.OrderDate >= from).GroupBy(o => o.OrderDate.Date).Select(g => new { g.Key, N = g.Count() }).ToList()
            .ToDictionary(x => x.Key.ToString("yyyy-MM-dd", En), x => x.N);
        var days = new List<string>();
        var dayCounts = new List<int>();
        for (var i = 0; i < 30; i++)
        {
            var d = from.AddDays(i);
            days.Add(d.ToString("dd MMM", En));
            dayCounts.Add(daily.GetValueOrDefault(d.ToString("yyyy-MM-dd", En)));
        }

         
        var months = new List<string>();
        var commission = new List<double>();
        var featured = new List<double>();
        for (var i = 5; i >= 0; i--)
        {
            var start = Clock.StartOfMonth(now).AddMonths(-i);
            var end = Clock.EndOfMonth(start);
            months.Add(start.ToString("MMM yyyy", En));
            commission.Add(Math.Round((double)(Db.Orders.Where(o => o.Status == "Completed" && o.CompletedDate >= start && o.CompletedDate <= end).Sum(o => (decimal?)o.CommissionFee) ?? 0m), 2));
            featured.Add(Math.Round((double)(Db.Payments.Where(p => p.PaymentType == "Featured fee" && p.Status == "Paid" && p.PaymentDate >= start && p.PaymentDate <= end).Sum(p => (decimal?)p.Amount) ?? 0m), 2));
        }

         
        var statusCounts = Db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        var byStatus = new[] { "Pending", "Confirmed", "Completed", "Cancelled" }.ToDictionary(s => s, s => statusCounts.GetValueOrDefault(s));
        var byCategory = (from c in Db.Categories
                          join l in Db.Listings.Where(x => x.Status == "Available") on c.CategoryID equals l.CategoryID into g
                          select new { name = c.CategoryName, n = g.Count() }).ToList().OrderByDescending(x => x.n).ToList();
        var topSellers = Db.Database.SqlQueryRaw<TopSellerRow>(
            @"SELECT TOP 5 s.StudentID, s.Username, s.FirstName, s.LastName, s.Major, AVG(CAST(r.Rating AS float)) AS Rating, COUNT(*) AS Reviews
              FROM reviews r JOIN order_items oi ON oi.OrderID = r.OrderID JOIN listings l ON l.ListingID = oi.ListingID JOIN students s ON s.StudentID = l.SellerID
              GROUP BY s.StudentID, s.Username, s.FirstName, s.LastName, s.Major
              ORDER BY Rating DESC, Reviews DESC").ToList();
        var orders = Db.Orders.Include(o => o.Buyer).Include(o => o.Payment)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
            .OrderByDescending(o => o.OrderDate).Take(6).ToList();

        var charts = new Dictionary<string, object?>
        {
            ["days"] = days, ["dayCounts"] = dayCounts,
            ["months"] = months, ["commission"] = commission, ["featured"] = featured,
            ["status"] = new Dictionary<string, object?> { ["labels"] = byStatus.Keys.ToList(), ["values"] = byStatus.Values.ToList() },
            ["category"] = new Dictionary<string, object?> { ["labels"] = byCategory.Select(x => x.name).ToList(), ["values"] = byCategory.Select(x => x.n).ToList() },
        };

        return View("admin.dashboard", new { kpis, tasks, charts, orders, topSellers });
    }

    private static string Trend(int now, int before)
    {
        if (before == 0)
        {
            return now > 0 ? "+" + now : "No change";
        }
        var pct = Math.Round((now - before) / (double)before * 100, MidpointRounding.AwayFromZero);
        return (pct >= 0 ? "+" : string.Empty) + pct.ToString("0", En);
    }
}
