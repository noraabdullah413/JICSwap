 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class BuyController : Controller
{
    [HttpGet("/listings/{listing:int}/buy", Name = "buy.create")]
    public async Task<IActionResult> Create(int listing)
    {
        var me = (await Me())!;
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller).FirstOrDefault(x => x.ListingID == listing)
            ?? throw new AbortException(404);
        var retry = RetryOrder(l, Request.Query["order"].ToString());
        string? why;
        if (retry == null && (why = Service<OrderService>().BlockReason(l, me)) != null)
        {
            TempData["error"] = why;    
            return RedirectToRoute("listings.show", new { listing = l.ListingID });
        }

        return View("student.buy", new { listing = l, retry });
    }

    [HttpPost("/listings/{listing:int}/buy", Name = "buy.store")]
    public async Task<IActionResult> Store(int listing, [FromForm] BuyRequest request)
    {
        var me = (await Me())!;
        var l = Db.Listings.Include(x => x.Seller).FirstOrDefault(x => x.ListingID == listing) ?? throw new AbortException(404);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var retry = RetryOrder(l, (request.Order ?? 0) != 0 ? request.Order.ToString()! : Request.Query["order"].ToString());    
        Order order;
        try
        {
            order = retry != null ? Service<OrderService>().Retry(retry, request) : Service<OrderService>().Place(l, me, request);
        }
        catch (InvalidOperationException e)    
        {
            TempData["error"] = e.Message;
            return RedirectToRoute("listings.show", new { listing = l.ListingID });
        }

        var payment = Db.Payments.First(p => p.OrderID == order.OrderID);    
        if (payment.IsOnline())
        {
            return RedirectToRoute("payments.redirect", new { payment = payment.PaymentID });
        }

        return RedirectToRoute("purchases.sent", new { order = order.OrderID });
    }

     
    private Order? RetryOrder(Listing listing, string? id)
    {
        if (string.IsNullOrEmpty(id) || !int.TryParse(id, out var orderId)) return null;

        var me = Me().GetAwaiter().GetResult()!;
        var order = Db.Orders.Include(o => o.Payment).Include(o => o.Items).FirstOrDefault(o => o.OrderID == orderId);
        var item = order?.Items.OrderBy(i => i.OrderItemID).FirstOrDefault();
        if (order == null || order.BuyerID != me.StudentID || order.Status != "Pending"
            || item == null || item.ListingID != listing.ListingID || order.Payment!.Status == "Paid")
        {
            return null;
        }

        return order;
    }
}
