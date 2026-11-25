 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class PurchaseController : Controller
{
    [HttpGet("/admin/purchases", Name = "admin.purchases.index")]
    public IActionResult Index([FromQuery] string? view, [FromQuery] string? status)
    {
        view = view == "payments" ? "payments" : "orders";
        object rows;
        if (view == "payments")
        {
            var q = Db.Payments.Include(p => p.Order).ThenInclude(o => o!.Items).ThenInclude(i => i.Listing).Include(p => p.Listing).AsQueryable();
            if (status is "Paid" or "Pending" or "Failed" or "Refunded" or "Cancelled") q = q.Where(p => p.Status == status);
            rows = PaginatedList<Payment>.Create(q.OrderByDescending(p => p.PaymentDate), Request, 15);
        }
        else
        {
            var q = Db.Orders.Include(o => o.Buyer).Include(o => o.Payment)
                .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
                .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
                .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images).AsQueryable();
            if (status is "Pending" or "Confirmed" or "Completed" or "Cancelled") q = q.Where(o => o.Status == status);
            rows = PaginatedList<Order>.Create(q.OrderByDescending(o => o.OrderDate), Request, 15);
        }
        var orderCounts = new Dictionary<string, int> { ["All"] = Db.Orders.Count() };
        foreach (var g in Db.Orders.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToList()) orderCounts[g.Key] = g.N;
        var payCounts = new Dictionary<string, int> { ["All"] = Db.Payments.Count() };
        foreach (var g in Db.Payments.GroupBy(p => p.Status).Select(g => new { g.Key, N = g.Count() }).ToList()) payCounts[g.Key] = g.N;

        return View("admin.purchases", new { rows, view, status, orderCounts, payCounts });
    }

    [HttpGet("/admin/purchases/{order:int}", Name = "admin.purchases.show")]
    public IActionResult Show(int order)
    {
        var o = Db.Orders.Include(x => x.Buyer).Include(x => x.Payment).Include(x => x.Review).Include(x => x.ReturnRequest)
            .Include(x => x.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
            .Include(x => x.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
            .Include(x => x.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
            .FirstOrDefault(x => x.OrderID == order) ?? throw new AbortException(404);
        var messages = Db.Messages.Thread(o.Listing!.ListingID, o.BuyerID, o.Listing.SellerID).Include(m => m.Sender).OrderBy(m => m.SentDate).ToList();

        return View("admin.purchase-show", new { order = o, messages });
    }

     
    [HttpGet("/admin/purchases/{order:int}/invoice", Name = "admin.purchases.invoice")]
    public IActionResult Invoice(int order)
    {
        var o = Db.Orders.Include(x => x.Buyer).Include(x => x.Payment).Include(x => x.ReturnRequest)
            .Include(x => x.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
            .Include(x => x.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
            .FirstOrDefault(x => x.OrderID == order) ?? throw new AbortException(404);

        return View("admin.print.invoice", new { order = o });
    }
}
