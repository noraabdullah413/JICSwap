 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class RequestController : Controller
{
    public static readonly Dictionary<string, string> TABS = new()
    {
        ["waiting"] = "Waiting for me", ["accepted"] = "Accepted", ["completed"] = "Completed", ["rejected"] = "Rejected",
    };

    [HttpGet("/requests", Name = "requests.index")]
    public async Task<IActionResult> Index([FromQuery] string? tab)
    {
        var me = (await Me())!;
        tab = tab != null && TABS.ContainsKey(tab) ? tab : "waiting";
        IQueryable<Order> Mine() => Db.Orders.Where(o => o.Items.Any(i => i.Listing!.SellerID == me.StudentID));
        var queries = new Dictionary<string, Func<IQueryable<Order>>>
        {
            ["waiting"] = () => Service<NotificationService>().WaitingRequestsQuery(me),
            ["accepted"] = () => Mine().Where(o => o.Status == "Confirmed"),
            ["completed"] = () => Mine().Where(o => o.Status == "Completed"),
            ["rejected"] = () => Mine().Where(o => o.Status == "Cancelled"),
        };
        var counts = queries.ToDictionary(kv => kv.Key, kv => kv.Value().Count());
        var orders = PaginatedList<Order>.Create(
            queries[tab]().Include(o => o.Buyer).Include(o => o.Payment)
                .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
                .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
                .OrderByDescending(o => o.OrderDate), Request, 10);
        var stats = Student.StatsFor(Db, orders.Select(o => o.BuyerID));

        return View("student.requests", new { orders, counts, tab, stats });
    }

    [HttpGet("/requests/{order:int}", Name = "requests.show")]
    public async Task<IActionResult> Show(int order)
    {
        var o = await Own(order);
        var note = Db.Messages.Where(m => m.ListingID == o.Listing!.ListingID && m.SenderID == o.BuyerID && m.SentDate == o.OrderDate)
            .OrderBy(m => m.MessageID).FirstOrDefault();
        var buyer = o.Buyer!;
        var buyerStats = buyer.SellerStats(Db);
        var buyerInfo = new Dictionary<string, object?>
        {
            ["purchases"] = Db.Orders.Count(x => x.BuyerID == buyer.StudentID && x.Status == "Completed"),
            ["complaints"] = Db.Complaints.Count(c => c.StudentID == buyer.StudentID && c.Status == "Resolved"),
        };

        return View("student.request-show", new { order = o, note, buyer, buyerStats, buyerInfo });
    }

    [HttpPost("/requests/{order:int}/accept", Name = "requests.accept")]
    public async Task<IActionResult> Accept(int order)
    {
        var o = await Own(order);
        if (o.Status != "Pending" || o.Listing!.Status != "Available")
        {
            TempData["error"] = "This request can no longer be accepted.";
            return Back();
        }
        var p = o.Payment!;
        if (p.IsOnline() && p.Status != "Paid")
        {
            TempData["error"] = "The buyer has not completed the payment yet.";
            return Back();
        }
        Service<OrderService>().Accept(o);

        TempData["flash"] = "You accepted the request. The buyer is notified and the meeting is confirmed.";
        return RedirectToRoute("requests.show", new { order = o.OrderID });
    }

    [HttpPost("/requests/{order:int}/reject", Name = "requests.reject")]
    public async Task<IActionResult> Reject(int order, [FromForm] RejectRequest request)
    {
        var o = await Own(order);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (o.Status != "Pending")
        {
            TempData["error"] = "Only a waiting request can be rejected.";
            return Back();
        }
        Service<OrderService>().Reject(o, "Request " + o.Reference + " was rejected: " + request.RejectReason!.Trim());

        TempData["flash"] = "The request was rejected. The buyer is refunded in full.";
        return RedirectToRoute("requests.index", new { tab = "rejected" });
    }

    [HttpPost("/requests/{order:int}/delivered", Name = "requests.delivered")]
    public async Task<IActionResult> Delivered(int order)
    {
        var o = await Own(order);
        if (o.Status != "Confirmed" || o.DeliveredDate != null)
        {
            TempData["error"] = "Only an accepted request can be marked as delivered.";
            return Back();
        }
        Service<OrderService>().MarkDelivered(o);

        TempData["flash"] = "Marked as delivered. The buyer now confirms receipt to complete the purchase.";
        return Back();
    }

     
    private async Task<Order> Own(int id)
    {
        var me = (await Me())!;
        var order = Db.Orders.Include(o => o.Buyer).Include(o => o.Payment)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
            .FirstOrDefault(o => o.OrderID == id) ?? throw new AbortException(404);
        AbortException.Unless(order.Listing?.SellerID == me.StudentID, 403);
        return order;
    }
}
