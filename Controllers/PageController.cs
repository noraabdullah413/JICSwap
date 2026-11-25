 
using Microsoft.AspNetCore.Mvc;
using JicSwap.Support;

namespace JicSwap.Controllers;

 
public class PageController : Controller
{
    [HttpGet("/how-it-works", Name = "pages.how")]
    public IActionResult How()
    {
        return View("public.how");
    }

    [HttpGet("/policies", Name = "pages.policies")]
    public IActionResult Policies()
    {
        return View("public.policies");
    }

    [HttpGet("/announcements", Name = "announcements.index")]
    public IActionResult Announcements([FromQuery] string? q)
    {
         
        q = (q ?? string.Empty).Trim();
        if (q.Length > 100) q = q.Substring(0, 100);
        var query = Db.Announcements.Where(a => a.Status == "Active");
        if (q != string.Empty)
        {
            query = query.Where(a => a.Title.Contains(q) || a.Message.Contains(q));
        }
        var rows = PaginatedList<Models.Announcement>.Create(query.OrderByDescending(a => a.PostedDate), Request, 12);
        var total = Db.Announcements.Count(a => a.Status == "Active");
        var latest = Db.Announcements.Where(a => a.Status == "Active").OrderByDescending(a => a.PostedDate).FirstOrDefault();

         
        HttpContext.Session.SetString("announcements_seen_at", Clock.Now().ToString("yyyy-MM-dd HH:mm:ss"));

        return View("public.announcements", new { rows, total, q, latest });
    }
}
