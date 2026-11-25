 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;

namespace JicSwap.Controllers;

 
[StudentActive]
public class ListingController : Controller
{
    [HttpGet("/listings/{listing:int}", Name = "listings.show")]
    public async Task<IActionResult> Show(int listing)
    {
        var me = await Me();
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller).FirstOrDefault(x => x.ListingID == listing)
            ?? throw new AbortException(404);
        var mine = me != null && l.SellerID == me.StudentID;
        if (me == null)
        {
             
            HttpContext.Session.SetString("url.intended", Request.Scheme + "://" + Request.Host + Request.Path + Request.QueryString);
        }

         
        if (!mine && (l.WaitingProof || l.Seller!.AccountStatus != "Active"))
        {
            throw new AbortException(404);
        }

         
        var seen = HttpContext.SessionGet<List<int>>("viewed") ?? new List<int>();
        if (!mine && !seen.Contains(l.ListingID))
        {
            l.ViewsCount++;    
            Db.SaveChanges();
            seen.Add(l.ListingID);
            HttpContext.SessionPut("viewed", seen);
        }

        var sellerStats = l.Seller!.SellerStats(Db);
        var reviews = Review.ForSeller(Db, l.SellerID).Take(3).ToList();
        var saved = me != null && Db.Favourites.Any(f => f.StudentID == me.StudentID && f.ListingID == l.ListingID);
        var similar = Db.Listings.Visible().Where(x => x.ListingID != l.ListingID && x.CategoryID == l.CategoryID)
            .Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller)
            .FeaturedFirst().ThenByDescending(x => x.ViewsCount).Take(3).ToList();
        if (similar.Count < 3)
        {
            var have = similar.Select(x => x.ListingID).ToList();
            var more = Db.Listings.Visible().Where(x => x.ListingID != l.ListingID && !have.Contains(x.ListingID) && x.ListingType == l.ListingType)
                .Include(x => x.Images).Include(x => x.Category).Include(x => x.Seller)
                .OrderByDescending(x => x.ViewsCount).Take(3 - similar.Count).ToList();
            similar = similar.Concat(more).ToList();
        }
        var stats = Student.StatsFor(Db, similar.Select(x => x.SellerID));
        var view = l.Status != "Available" ? "student.listing-unavailable"
            : (l.IsService() ? "student.listing-service" : "student.listing-item");

         
        Order? holdOrder = null;
        if (l.Status is "Reserved" or "Sold")
        {
            var wanted = l.Status == "Reserved" ? "Confirmed" : "Completed";
            holdOrder = Db.Orders.Where(o => o.Status == wanted && o.Items.Any(i => i.ListingID == l.ListingID)).OrderByDescending(o => o.OrderDate).FirstOrDefault();    
        }

        return View(view, new { listing = l, mine, sellerStats, reviews, saved, similar, stats, holdOrder });
    }
}
