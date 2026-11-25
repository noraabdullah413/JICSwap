 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Services;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class ListingController : Controller
{
    public static readonly Dictionary<string, string> TABS = new()
    {
        ["all"] = "All", ["proof"] = "Waiting for proof", ["items"] = "Items", ["services"] = "Services", ["Available"] = "Available",
        ["Reserved"] = "Reserved", ["Sold"] = "Sold", ["Inactive"] = "Inactive",
    };

    [HttpGet("/admin/listings", Name = "admin.listings.index")]
    public IActionResult Index([FromQuery] string? tab)
    {
        tab = tab != null && TABS.ContainsKey(tab) ? tab : "all";
        IQueryable<Listing> Apply(IQueryable<Listing> q, string t) => t switch
        {
            "proof" => q.Where(l => l.ListingType == "Service" && l.ProofVerifiedAt == null && l.Status == "Available"),
            "items" => q.Where(l => l.ListingType == "Item"),
            "services" => q.Where(l => l.ListingType == "Service"),
            "Available" or "Reserved" or "Sold" or "Inactive" => q.Where(l => l.Status == t),
            _ => q,
        };
        var counts = TABS.Keys.ToDictionary(t => t, t => Apply(Db.Listings, t).Count());
         
        var listings = PaginatedList<Listing>.Create(
            Apply(Db.Listings.Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller), tab)
                .OrderBy(l => l.ProofVerifiedAt == null && l.ListingType == "Service" && l.Status == "Available" ? 0 : 1)
                .ThenByDescending(l => l.PostedDate), Request, 15);
        foreach (var l in listings) l.OrderItemsCount = Db.OrderItems.Count(i => i.ListingID == l.ListingID);

        return View("admin.listings", new { listings, counts, tab });
    }

    [HttpGet("/admin/listings/{listing:int}", Name = "admin.listings.show")]
    public IActionResult Show(int listing)
    {
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller).FirstOrDefault(x => x.ListingID == listing)
            ?? throw new AbortException(404);
        var orders = Db.Orders.Include(o => o.Buyer).Include(o => o.Payment).Where(o => o.Items.Any(i => i.ListingID == l.ListingID))
            .OrderByDescending(o => o.OrderDate).ToList();
        var complaints = Db.Complaints.Where(c => c.ListingID == l.ListingID).Include(c => c.Reporter).OrderByDescending(c => c.ReportDate).ToList();
        var proofDocs = l.IsService() ? Service<UploadsDisk>().Files("proofs/" + l.ListingID).ToList() : new List<string>();

        return View("admin.listing-show", new { listing = l, orders, complaints, proofDocs, sellerStats = l.Seller!.SellerStats(Db) });
    }

    [HttpPost("/admin/listings/{listing:int}/verify", Name = "admin.listings.verify")]
    public IActionResult Verify(int listing)
    {
        var l = FindOrFail<Listing>(listing);
        AbortException.Unless(l.IsService(), 404);
        l.ProofVerifiedAt = Clock.StartOfSecond();
        Db.SaveChanges();

        TempData["flash"] = "The proof of experience is verified. The service is now visible in the marketplace.";
        return Back();
    }

    [HttpPost("/admin/listings/{listing:int}/remove", Name = "admin.listings.remove")]
    public async Task<IActionResult> Remove(int listing)
    {
        var l = FindOrFail<Listing>(listing);
        Service<AdminActions>().RemoveListing(l, (await AdminUser())!);

        TempData["flash"] = "The listing was removed (Inactive). Its open orders were cancelled and its open complaints resolved with “Listing removed”.";
        return Back();
    }
}
