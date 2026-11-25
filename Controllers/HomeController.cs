 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;

namespace JicSwap.Controllers;

 
public class HomeController : Controller
{
    [HttpGet("/", Name = "home")]
    public IActionResult Index()
    {
         
        IQueryable<Listing> Visible() => Db.Listings.Visible();
        var weekAgo = Clock.Now().AddDays(-7);
        var kpi = new Dictionary<string, object?>
        {
            ["available"] = Visible().Count(),
            ["week"] = Visible().Count(l => l.PostedDate >= weekAgo),
            ["services"] = Visible().Count(l => l.ListingType == "Service"),
            ["students"] = Db.Students.Count(s => s.AccountStatus == "Active"),
            ["completed"] = Db.Orders.Count(o => o.Status == "Completed"),
        };
         
        var counts = Visible().GroupBy(l => l.CategoryID).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
         
        var categories = Db.Categories.OrderBy(c => c.CategoryType == "Item" ? 0 : 1).ThenBy(c => c.CategoryID).ToList();
        var featured = Visible().Where(l => l.ListingType == "Item").Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller)
            .FeaturedFirst().ThenByDescending(l => l.PostedDate).Take(4).ToList();
        var services = Visible().Where(l => l.ListingType == "Service").Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller)
            .FeaturedFirst().ThenByDescending(l => l.ViewsCount).Take(3).ToList();
        var stats = Student.StatsFor(Db, featured.Select(l => l.SellerID).Concat(services.Select(l => l.SellerID)));
        var announcements = Db.Announcements.Where(a => a.Status == "Active").OrderByDescending(a => a.PostedDate).Take(3).ToList();

        return View("public.landing", new { kpi, counts, categories, featured, services, stats, announcements });
    }
}
