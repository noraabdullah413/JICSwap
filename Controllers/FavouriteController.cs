 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class FavouriteController : Controller
{
    [HttpGet("/favourites", Name = "favourites.index")]
    public async Task<IActionResult> Index()
    {
        var me = (await Me())!;
        var favs = Db.Favourites.Where(f => f.StudentID == me.StudentID)
            .Include(f => f.Listing).ThenInclude(l => l!.Images)
            .Include(f => f.Listing).ThenInclude(l => l!.Category)
            .Include(f => f.Listing).ThenInclude(l => l!.Seller)
            .OrderByDescending(f => f.DateAdded).ToList().Where(f => f.Listing != null).ToList();    
        var stats = Student.StatsFor(Db, favs.Select(f => f.Listing!.SellerID));
        var popular = new List<Listing>();
        if (favs.Count == 0)
        {
            popular = Db.Listings.Visible().Where(l => l.SellerID != me.StudentID)
                .Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller).OrderByDescending(l => l.ViewsCount).Take(3).ToList();
            stats = Student.StatsFor(Db, popular.Select(l => l.SellerID));
        }

        return View("student.favourites", new { favs, stats, popular });
    }

    [HttpPost("/listings/{listing:int}/favourite", Name = "favourites.toggle")]
    public async Task<IActionResult> Toggle(int listing)
    {
        var me = (await Me())!;
        var l = FindOrFail<Listing>(listing);
        AbortException.If(l.SellerID == me.StudentID, 403, "You cannot save your own listing.");

        var fav = Db.Favourites.FirstOrDefault(f => f.StudentID == me.StudentID && f.ListingID == l.ListingID);
        string msg;
        if (fav != null)
        {
            Db.Favourites.Remove(fav);
            msg = "Removed from your favourites.";
        }
        else
        {
            Db.Favourites.Add(new Favourite { StudentID = me.StudentID, ListingID = l.ListingID, DateAdded = Clock.StartOfSecond() });
            msg = "Saved to your favourites.";
        }
        Db.SaveChanges();

        TempData["flash"] = msg;
        return Back();
    }
}
