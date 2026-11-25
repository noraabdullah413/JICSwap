 
using System.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.Support.Services.Payments;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class PaymentController : Controller
{
    private readonly IPaymentGateway _gateway;

    public PaymentController(IPaymentGateway gateway)
    {
        _gateway = gateway;
    }

    private Listing? ListingOfPayment(Payment p) =>
        p.PaymentType == "Order"
            ? Db.OrderItems.Include(i => i.Listing).Where(i => i.OrderID == p.OrderID).OrderBy(i => i.OrderItemID).Select(i => i.Listing).FirstOrDefault()
            : Db.Listings.FirstOrDefault(l => l.ListingID == p.ListingID);

     
    [HttpGet("/payments/{payment:int}/redirect", Name = "payments.redirect")]
    public async Task<IActionResult> Redirect_(int payment)
    {
        var p = FindOrFail<Payment>(payment);
        await AuthorizePayment(p);
        if (p.Status != "Pending")
        {
            return AfterResult(p);
        }

        var start = await _gateway.Start(p);
         
        var qIndex = start.Action.IndexOf('?');
        if (start.Method == "GET" && qIndex >= 0)
        {
            var parsed = HttpUtility.ParseQueryString(start.Action.Substring(qIndex + 1));
            foreach (var key in parsed.AllKeys.Where(k => k != null)) start.Fields[key!] = parsed[key] ?? string.Empty;
            start.Action = start.Action.Substring(0, qIndex);
        }

        return View("student.payment-redirect", new { payment = p, start, listing = ListingOfPayment(p) });
    }

     
    [HttpGet("/payments/{payment:int}/gateway", Name = "payments.gateway")]
    public async Task<IActionResult> Gateway(int payment)
    {
        AbortException.Unless(_gateway is FakeGateway, 404);
        var p = FindOrFail<Payment>(payment);
        await AuthorizePayment(p);
        AbortException.Unless(p.Status == "Pending", 404);

        return View("student.payment-gateway", new { payment = p, listing = ListingOfPayment(p) });
    }

    [HttpPost("/payments/{payment:int}/gateway", Name = "payments.gateway.submit")]
    public async Task<IActionResult> GatewaySubmit(int payment, [FromForm] string? decision)
    {
        AbortException.Unless(_gateway is FakeGateway, 404);
        var p = FindOrFail<Payment>(payment);
        await AuthorizePayment(p);

        var id = ((FakeGateway)_gateway).Decide(p, decision == "pay");
        return RedirectToRoute("payments.callback", new { payment = p.PaymentID, id });
    }

     
    [HttpGet("/payments/{payment:int}/callback", Name = "payments.callback")]
    public async Task<IActionResult> Callback(int payment)
    {
        var p = FindOrFail<Payment>(payment);
        await AuthorizePayment(p);

        if (p.Status == "Pending")
        {
            var query = Request.Query.ToDictionary(k => k.Key, k => k.Value.ToString());
            var result = await _gateway.Verify(p, query);
            if (result != null)
            {
                if (p.PaymentType == "Order")
                {
                    Service<OrderService>().PaymentResult(p, result.Paid, result.Reference);
                }
                else
                {
                    Service<FeatureService>().Result(p, result.Paid, result.Reference);
                }
            }
        }

        Db.Entry(p).Reload();    
        return AfterResult(p);
    }

    [HttpGet("/purchases/{order:int}/payment-failed", Name = "purchases.failed")]
    public async Task<IActionResult> Failed(int order)
    {
        var me = (await Me())!;
        var o = FindOrFail<Order>(order);
        AbortException.Unless(o.BuyerID == me.StudentID, 403);
        var payment = Db.Payments.First(p => p.OrderID == o.OrderID);
        if (payment.Status == "Paid" || o.Status != "Pending")
        {
            return RedirectToRoute("purchases.show", new { order = o.OrderID });
        }

        var listing = Db.OrderItems.Include(i => i.Listing).Where(i => i.OrderID == o.OrderID).OrderBy(i => i.OrderItemID).Select(i => i.Listing).FirstOrDefault();
        return View("student.payment-failed", new { order = o, payment, listing });
    }

    private IActionResult AfterResult(Payment p)
    {
        if (p.PaymentType == "Order")
        {
            return p.Status == "Paid"
                ? RedirectToRoute("purchases.sent", new { order = p.OrderID })
                : RedirectToRoute("purchases.failed", new { order = p.OrderID });
        }

        if (p.Status == "Paid")
        {
            return RedirectToRoute("my-listings.featured", new { listing = p.ListingID, payment = p.PaymentID });
        }
        TempData["pay_failed"] = p.PaymentID;    
        return RedirectToRoute("my-listings.feature", new { listing = p.ListingID });
    }

    private async Task AuthorizePayment(Payment p)
    {
        var me = (await Me())!.StudentID;
        int? owner = p.PaymentType == "Order"
            ? Db.Orders.Where(o => o.OrderID == p.OrderID).Select(o => (int?)o.BuyerID).FirstOrDefault()
            : Db.Listings.Where(l => l.ListingID == p.ListingID).Select(l => (int?)l.SellerID).FirstOrDefault();
        AbortException.Unless(owner == me, 403);
    }
}
