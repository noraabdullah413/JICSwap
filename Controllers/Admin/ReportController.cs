 
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JicSwap.Support;
using JicSwap.Support.Services;
using JicSwap.ViewModels.Admin;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class ReportController : Controller
{
     
    public static readonly Dictionary<string, string[]> REPORTS = new()
    {
        ["sales"] = new[] { "Sales and commission", "Completed sales, sales amount and the 5% commission revenue by month.", "bi-graph-up-arrow" },
        ["featured"] = new[] { "Featured listing revenue", "Every SAR 15 featured listing fee paid in the period.", "bi-star" },
        ["sellers"] = new[] { "Top rated sellers", "Sellers ranked by the average rating of their reviews.", "bi-trophy" },
        ["categories"] = new[] { "Most active categories", "Listings posted, orders and sales per category.", "bi-tags" },
        ["complaints"] = new[] { "Complaints summary", "Complaints by status and by type (listing, service, user).", "bi-flag" },
        ["returns"] = new[] { "Returns and refunds", "Return requests by status and the amount refunded.", "bi-arrow-counterclockwise" },
    };

    [HttpGet("/admin/reports", Name = "admin.reports")]
    public IActionResult Index([FromQuery] ReportFilterRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var data = Data(request);
        data["reports"] = REPORTS;
        return View("admin.reports", data);
    }

    [HttpGet("/admin/reports/print", Name = "admin.reports.print")]
    public IActionResult Print([FromQuery] ReportFilterRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var only = request.Report != null && REPORTS.ContainsKey(request.Report) ? request.Report : null;
        var data = Data(request);
        data["reports"] = REPORTS;
        data["only"] = only;
        return View("admin.print.report", data);
    }

     
    private Dictionary<string, object?> Data(ReportFilterRequest request)
    {
        var preset = request.Preset;
        var now = Clock.Now();
        var today = Clock.Today();
        var semester = new DateTime(2026, 8, 1);    
        DateTime from, to;
        switch (preset)
        {
            case "month": (from, to) = (Clock.StartOfMonth(now), today); break;
            case "last-month": (from, to) = (Clock.StartOfMonth(Clock.SubMonthsNoOverflow(now, 1)), Clock.EndOfMonth(Clock.SubMonthsNoOverflow(now, 1))); break;
            case "30d": (from, to) = (today.AddDays(-29), today); break;
            case "year": (from, to) = (new DateTime(now.Year, 1, 1), today); break;
            case "semester": (from, to) = (semester, today); break;
            default:
                from = !string.IsNullOrEmpty(request.From) ? DateTime.Parse(request.From, CultureInfo.InvariantCulture) : semester;
                to = !string.IsNullOrEmpty(request.To) ? DateTime.Parse(request.To, CultureInfo.InvariantCulture) : today;
                break;
        }
        if (string.IsNullOrEmpty(preset) && string.IsNullOrEmpty(request.From) && string.IsNullOrEmpty(request.To))
        {
            preset = "semester";
        }
        var r = new ReportService(Db, from, to);

        return new Dictionary<string, object?>
        {
            ["from"] = from, ["to"] = to, ["preset"] = preset, ["sales"] = r.Sales(), ["featured"] = r.Featured(),
            ["sellers"] = r.TopSellers(), ["categories"] = r.Categories(), ["complaints"] = r.Complaints(), ["returns"] = r.Returns(),
        };
    }
}
