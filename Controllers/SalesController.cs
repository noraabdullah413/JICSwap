 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class SalesController : Controller
{
    [HttpGet("/sales", Name = "sales.index")]
    public async Task<IActionResult> Index()
    {
        var me = (await Me())!;
        var q = Db.Orders.Where(o => o.Status == "Completed" && o.Items.Any(i => i.Listing!.SellerID == me.StudentID));
        var all = q.Select(o => new { o.Amount, o.CommissionFee }).ToList();    
        var tiles = new Dictionary<string, object?>
        {
            ["count"] = all.Count,
            ["gross"] = (double)all.Sum(o => o.Amount),
            ["commission"] = (double)all.Sum(o => o.CommissionFee),
            ["earnings"] = (double)all.Sum(o => o.Amount - o.CommissionFee),
        };
        var orders = PaginatedList<Order>.Create(
            q.Include(o => o.Buyer).Include(o => o.Payment)
             .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
             .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
             .OrderByDescending(o => o.CompletedDate), Request, 10);

        return View("student.sales", new { tiles, orders });
    }
}
