 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.ViewModels.Admin;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class AnnouncementController : Controller
{
    [HttpGet("/admin/announcements", Name = "admin.announcements.index")]
    public IActionResult Index()
    {
        var rows = Db.Announcements.Include(a => a.Admin).OrderBy(a => a.Status == "Active" ? 0 : 1).ThenByDescending(a => a.PostedDate).ToList();
        return View("admin.announcements", new { rows });
    }

    [HttpPost("/admin/announcements", Name = "admin.announcements.store")]
    public async Task<IActionResult> Store([FromForm] AnnouncementRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        Db.Announcements.Add(new Announcement
        {
            AdminID = (await AdminUser())!.AdminID, Title = request.Title!.Trim(), Message = request.Message!.Trim(),
            PostedDate = Clock.StartOfSecond(), Status = "Active",
        });
        Db.SaveChanges();

        TempData["flash"] = "The announcement is published on the home page and in the students’ notifications.";
        return RedirectToRoute("admin.announcements.index");
    }

    [HttpPost("/admin/announcements/{announcement:int}/archive", Name = "admin.announcements.archive")]
    public IActionResult Archive(int announcement)
    {
        var a = FindOrFail<Announcement>(announcement);
        a.Status = "Archived";
        Db.SaveChanges();

        TempData["flash"] = a.Reference + " was archived.";
        return Back();
    }
}
