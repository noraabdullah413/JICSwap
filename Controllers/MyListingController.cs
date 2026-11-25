 
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
public class MyListingController : Controller
{
    public static readonly string[] TABS = { "All", "Available", "Reserved", "Sold", "Inactive" };

    [HttpGet("/my-listings", Name = "my-listings.index")]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var me = (await Me())!;
        status = TABS.Contains(status) ? status! : "All";
        var baseQ = Db.Listings.Where(l => l.SellerID == me.StudentID);
        var counts = new Dictionary<string, int> { ["All"] = baseQ.Count() };
        foreach (var g in baseQ.GroupBy(l => l.Status).Select(g => new { g.Key, N = g.Count() }).ToList()) counts[g.Key] = g.N;

         
        var q = baseQ.Include(l => l.Images).Include(l => l.Category)
            .OrderBy(l => l.Status == "Available" ? 0 : l.Status == "Reserved" ? 1 : l.Status == "Sold" ? 2 : 3)
            .ThenByDescending(l => l.PostedDate).AsQueryable();
        if (status != "All")
        {
            q = q.Where(l => l.Status == status);
        }
        var listings = q.ToList();
        foreach (var l in listings) l.OrderItemsCount = Db.OrderItems.Count(i => i.ListingID == l.ListingID);

        var sellerOrders = Db.Orders.Where(o => o.Items.Any(i => i.Listing!.SellerID == me.StudentID));
        var stats = new Dictionary<string, object?>
        {
            ["active"] = counts.GetValueOrDefault("Available"),
            ["views"] = baseQ.Sum(l => (int?)l.ViewsCount) ?? 0,
            ["open"] = sellerOrders.Count(o => o.Status == "Pending" || o.Status == "Confirmed"),
            ["earnings"] = (double)(sellerOrders.Where(o => o.Status == "Completed").Sum(o => (decimal?)(o.Amount - o.CommissionFee)) ?? 0m),    
        };

        return View("student.my-listings", new { listings, counts, status, stats });
    }

    [HttpGet("/my-listings/{listing:int}", Name = "my-listings.show")]
    public async Task<IActionResult> Show(int listing)
    {
        var l = await Own(listing);
        var orders = Db.Orders.Include(o => o.Buyer).Include(o => o.Payment).Where(o => o.Items.Any(i => i.ListingID == l.ListingID))
            .OrderByDescending(o => o.OrderDate).ToList();
        var featurePays = Db.Payments.Where(p => p.ListingID == l.ListingID && p.PaymentType == "Featured fee" && p.Status == "Paid")
            .OrderByDescending(p => p.PaymentDate).ToList();
        var tiles = new Dictionary<string, object?>
        {
            ["favourites"] = Db.Favourites.Count(f => f.ListingID == l.ListingID),
            ["requests"] = orders.Count,
            ["waiting"] = orders.Count(o => o.Status == "Pending"),
            ["messages"] = Db.Messages.Count(m => m.ListingID == l.ListingID),
        };
        var proofDocs = l.IsService() ? Service<UploadsDisk>().Files("proofs/" + l.ListingID).ToList() : new List<string>();

        return View("student.my-listing-show", new { listing = l, orders, featurePays, tiles, proofDocs });
    }

    [HttpGet("/my-listings/{listing:int}/published", Name = "my-listings.published")]
    public async Task<IActionResult> Published(int listing)
    {
        var l = await Own(listing);
        return View("student.my-listing-published", new { listing = l });
    }

     
    [HttpPost("/my-listings/{listing:int}/status", Name = "my-listings.status")]
    public async Task<IActionResult> Status(int listing, [FromForm] ListingStatusRequest request)
    {
        var l = await Own(listing);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (l.Status is "Reserved" or "Sold")
        {
            TempData["error"] = "A reserved or sold listing changes status automatically.";
            return Back();
        }
        var @new = request.Status!;
        if (@new == "Inactive" && Db.Orders.Any(o => o.Status == "Pending" && o.Items.Any(i => i.ListingID == l.ListingID)))
        {
            TempData["error"] = "Answer the waiting purchase requests before you make this listing inactive.";
            return Back();
        }
        l.Status = @new;
        Db.SaveChanges();

        TempData["flash"] = @new == "Available" ? "The listing is visible to other students again." : "The listing is hidden from other students.";
        return Back();
    }

     
    [HttpDelete("/my-listings/{listing:int}", Name = "my-listings.destroy")]
    public async Task<IActionResult> Destroy(int listing)
    {
        var l = await Own(listing);
        if (l.Status == "Reserved"
            || Db.Orders.Any(o => (o.Status == "Pending" || o.Status == "Confirmed") && o.Items.Any(i => i.ListingID == l.ListingID)))
        {
            TempData["error"] = "This listing has an open purchase request and cannot be removed now.";
            return Back();
        }
        if (l.IsReferenced(Db))
        {
            l.Status = "Inactive";
            Db.SaveChanges();
            TempData["flash"] = "The listing has requests, messages or favourites, so it was set to Inactive and hidden from other students.";
            return RedirectToRoute("my-listings.index");
        }
        using (var tx = Db.Database.BeginTransaction())
        {
            Db.Images.RemoveRange(Db.Images.Where(i => i.ListingID == l.ListingID));
            Db.Listings.Remove(l);
            Db.SaveChanges();
            tx.Commit();
        }
        var disk = Service<UploadsDisk>();
        disk.DeleteDirectory("listings/" + l.ListingID);
        disk.DeleteDirectory("proofs/" + l.ListingID);

        TempData["flash"] = "The listing was deleted.";
        return RedirectToRoute("my-listings.index");
    }

    [HttpGet("/my-listings/{listing:int}/feature", Name = "my-listings.feature")]
    public async Task<IActionResult> FeatureForm(int listing)
    {
        var l = await Own(listing);
        var why = CannotFeature(l);
        if (why != null)
        {
            TempData["error"] = why;
            return RedirectToRoute("my-listings.show", new { listing = l.ListingID });
        }
        var failed = TempData["pay_failed"] is { } pf && int.TryParse(pf.ToString(), out var pid) ? Db.Payments.Find(pid) : null;

        return View("student.feature", new { listing = l, until = Service<FeatureService>().UntilIfPaid(l), failed });
    }

    [HttpPost("/my-listings/{listing:int}/feature", Name = "my-listings.feature.pay")]
    public async Task<IActionResult> FeaturePay(int listing, [FromForm] FeatureRequest request)
    {
        var l = await Own(listing);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var why = CannotFeature(l);
        if (why != null)
        {
            TempData["error"] = why;
            return RedirectToRoute("my-listings.show", new { listing = l.ListingID });
        }
        var payment = Service<FeatureService>().Start(l, request.PaymentMethod!);

        return RedirectToRoute("payments.redirect", new { payment = payment.PaymentID });
    }

    [HttpGet("/my-listings/{listing:int}/featured", Name = "my-listings.featured")]
    public async Task<IActionResult> Featured(int listing, [FromQuery] int? payment)
    {
        var l = await Own(listing);
        var q = Db.Payments.Where(p => p.ListingID == l.ListingID && p.PaymentType == "Featured fee" && p.Status == "Paid");
        if (payment != null) q = q.Where(p => p.PaymentID == payment);    
        var pay = q.OrderByDescending(p => p.PaymentDate).FirstOrDefault() ?? throw new AbortException(404);    

        return View("student.featured", new { listing = l, payment = pay });
    }

    private static string? CannotFeature(Listing l)
    {
        if (l.Status != "Available")
        {
            return "Only an available listing can be featured.";
        }
        if (l.WaitingProof)
        {
            return "A service can be featured after the admin has checked the proof of experience.";
        }

        return null;
    }

     
    private async Task<Listing> Own(int id)
    {
        var me = (await Me())!;
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).FirstOrDefault(x => x.ListingID == id) ?? throw new AbortException(404);
        AbortException.Unless(l.SellerID == me.StudentID, 403);
        return l;
    }
}
