 
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
public class PurchaseController : Controller
{
    public static readonly string[] TABS = { "All", "Pending", "Confirmed", "Completed", "Cancelled" };

     
    private IQueryable<Order> WithListing(IQueryable<Order> q) =>
        q.Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
         .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
         .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
         .Include(o => o.Payment).Include(o => o.Review);

    [HttpGet("/purchases", Name = "purchases.index")]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var me = (await Me())!;
        status = TABS.Contains(status) ? status! : "All";
        var baseQ = Db.Orders.Where(o => o.BuyerID == me.StudentID);
        var counts = new Dictionary<string, int> { ["All"] = baseQ.Count() };
        foreach (var g in baseQ.GroupBy(o => o.Status).Select(g => new { g.Key, N = g.Count() }).ToList()) counts[g.Key] = g.N;

        var q = WithListing(baseQ).OrderByDescending(o => o.OrderDate).AsQueryable();
        if (status != "All")
        {
            q = q.Where(o => o.Status == status);
        }
        var orders = PaginatedList<Order>.Create(q, Request, 10);

        var popular = new List<Listing>();
        var stats = new Dictionary<int, Student.SellerStatsRow>();
        if (counts["All"] == 0)
        {
            popular = Db.Listings.Visible().Where(l => l.SellerID != me.StudentID && l.Seller!.Major == me.Major)
                .Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller).OrderByDescending(l => l.ViewsCount).Take(3).ToList();
            if (popular.Count < 3)
            {
                var have = popular.Select(l => l.ListingID).ToList();
                popular = popular.Concat(Db.Listings.Visible().Where(l => l.SellerID != me.StudentID && !have.Contains(l.ListingID))
                    .Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller)
                    .OrderByDescending(l => l.ViewsCount).Take(3 - popular.Count).ToList()).ToList();
            }
            stats = Student.StatsFor(Db, popular.Select(l => l.SellerID));
        }

        return View("student.purchases", new { orders, counts, status, popular, stats });
    }

     
    [HttpGet("/purchases/print", Name = "purchases.print")]
    public async Task<IActionResult> Print()
    {
        var me = (await Me())!;
        var orders = WithListing(Db.Orders.Where(o => o.BuyerID == me.StudentID)).OrderByDescending(o => o.OrderDate).ToList();
        return View("student.purchases-print", new { orders });
    }

    [HttpGet("/purchases/{order:int}", Name = "purchases.show")]
    public async Task<IActionResult> Show(int order)
    {
        var o = await Own(order, q => WithListing(q).Include(x => x.ReturnRequest));
        var sellerStats = o.Listing!.Seller!.SellerStats(Db);
        return View("student.purchase-show", new { order = o, sellerStats });
    }

    [HttpGet("/purchases/{order:int}/sent", Name = "purchases.sent")]
    public async Task<IActionResult> Sent(int order)
    {
        var o = await Own(order, WithListing);
        var sellerStats = o.Listing!.Seller!.SellerStats(Db);
        return View("student.purchase-sent", new { order = o, sellerStats });
    }

    [HttpPost("/purchases/{order:int}/cancel", Name = "purchases.cancel")]
    public async Task<IActionResult> Cancel(int order)
    {
        var o = await Own(order);
        if (o.Status != "Pending")
        {
            TempData["error"] = "A purchase can be cancelled only before the seller accepts it.";
            return Back();
        }
        Service<OrderService>().Cancel(o);
        TempData["flash"] = "The purchase was cancelled. Any amount you paid online is refunded.";
        return RedirectToRoute("purchases.show", new { order = o.OrderID });
    }

    [HttpPost("/purchases/{order:int}/confirm", Name = "purchases.confirm")]
    public async Task<IActionResult> Confirm(int order)
    {
        var o = await Own(order);
        if (o.Status != "Confirmed")
        {
            TempData["error"] = "Only a purchase confirmed by the seller can be completed.";
            return Back();
        }
        Service<OrderService>().ConfirmReceipt(o);
        TempData["flash"] = "Thank you. The purchase is completed. You can now review the seller.";
        return RedirectToRoute("purchases.review", new { order = o.OrderID });
    }

    [HttpGet("/purchases/{order:int}/review", Name = "purchases.review")]
    public async Task<IActionResult> ReviewForm(int order)
    {
        var o = await Own(order, WithListing);
        if (o.Status != "Completed" || o.Review != null)
        {
            TempData["error"] = o.Review != null ? "You have already reviewed this purchase." : "You can review a purchase once it is completed.";
            return RedirectToRoute("purchases.show", new { order = o.OrderID });
        }
        return View("student.review", new { order = o });
    }

    [HttpPost("/purchases/{order:int}/review", Name = "purchases.review.store")]
    public async Task<IActionResult> ReviewStore(int order, [FromForm] ReviewRequest request)
    {
        var o = await Own(order);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (o.Status != "Completed" || Db.Reviews.Any(r => r.OrderID == o.OrderID))
        {
            return RedirectToRoute("purchases.show", new { order = o.OrderID });
        }
        Db.Reviews.Add(new Review
        {
            OrderID = o.OrderID, Rating = request.Rating!.Value,
            Comment = string.IsNullOrEmpty(request.Comment) ? null : request.Comment, ReviewDate = Clock.StartOfSecond(),
        });
        Db.SaveChanges();

        TempData["flash"] = "Thank you for your review.";
        return RedirectToRoute("purchases.show", new { order = o.OrderID });
    }

    [HttpGet("/purchases/{order:int}/return", Name = "purchases.return")]
    public async Task<IActionResult> ReturnForm(int order)
    {
        var o = await Own(order, q => WithListing(q).Include(x => x.ReturnRequest));
        if (o.Status != "Completed" || o.ReturnRequest != null)
        {
            TempData["error"] = o.ReturnRequest != null ? "A return request was already sent for this purchase." : "A return can be requested only for a completed purchase.";
            return RedirectToRoute("purchases.show", new { order = o.OrderID });
        }
        return View("student.return", new { order = o });
    }

    [HttpPost("/purchases/{order:int}/return", Name = "purchases.return.store")]
    public async Task<IActionResult> ReturnStore(int order, [FromForm] ReturnFormRequest request)
    {
        var o = await Own(order);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (o.Status != "Completed" || Db.ReturnRequests.Any(r => r.OrderID == o.OrderID))
        {
            return RedirectToRoute("purchases.show", new { order = o.OrderID });
        }
        var path = await Service<UploadsDisk>().Store(request.EvidenceFile!, "returns/" + o.OrderID);
        Db.ReturnRequests.Add(new ReturnRequest
        {
            OrderID = o.OrderID, Reason = request.Reason!, EvidenceImage = path, RequestDate = Clock.StartOfSecond(), Status = "Pending",
        });
        Db.SaveChanges();

        TempData["flash"] = "Your return request was sent. The admin will review it and you get a notification with the decision.";
        return RedirectToRoute("purchases.show", new { order = o.OrderID });
    }

     
    private async Task<Order> Own(int id, Func<IQueryable<Order>, IQueryable<Order>>? with = null)
    {
        var me = (await Me())!;
        var q = with != null ? with(Db.Orders) : Db.Orders;
        var order = q.FirstOrDefault(o => o.OrderID == id) ?? throw new AbortException(404);
        AbortException.Unless(order.BuyerID == me.StudentID, 403);
        return order;
    }
}
