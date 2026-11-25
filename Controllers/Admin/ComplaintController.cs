 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Services;
using JicSwap.ViewModels.Admin;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class ComplaintController : Controller
{
    public static readonly string[] TABS = { "Pending", "Under Review", "Resolved", "Dismissed" };

    [HttpGet("/admin/complaints", Name = "admin.complaints.index")]
    public IActionResult Index([FromQuery] string? status)
    {
        status = TABS.Contains(status) ? status! : "open";
        var counts = Db.Complaints.GroupBy(c => c.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N);
        counts["open"] = counts.GetValueOrDefault("Pending") + counts.GetValueOrDefault("Under Review");
        var q = Db.Complaints.Include(c => c.Reporter).Include(c => c.Student).Include(c => c.Listing).AsQueryable();
        q = status == "open" ? q.Where(c => c.Status == "Pending" || c.Status == "Under Review") : q.Where(c => c.Status == status);
        var complaints = PaginatedList<Complaint>.Create(q.OrderByDescending(c => c.ReportDate), Request, 15);

        return View("admin.complaints", new { complaints, counts, status });
    }

    [HttpGet("/admin/complaints/{complaint:int}", Name = "admin.complaints.show")]
    public IActionResult Show(int complaint)
    {
        var c = Db.Complaints.Include(x => x.Reporter).Include(x => x.Student)
            .Include(x => x.Listing).ThenInclude(l => l!.Images).Include(x => x.Listing).ThenInclude(l => l!.Category)
            .Include(x => x.Admin).FirstOrDefault(x => x.ComplaintID == complaint) ?? throw new AbortException(404);
        List<Message> messages;
        if (c.ListingID != null)
        {
            messages = Db.Messages.Thread(c.ListingID.Value, c.ReporterID, c.StudentID).Include(m => m.Sender).OrderBy(m => m.SentDate).ToList();
        }
        else
        {
            messages = Db.Messages.Where(m => (m.SenderID == c.ReporterID && m.ReceiverID == c.StudentID) || (m.SenderID == c.StudentID && m.ReceiverID == c.ReporterID))
                .Include(m => m.Sender).Include(m => m.Listing).OrderBy(m => m.SentDate).ToList();
        }
        var previous = Db.Complaints.Count(x => x.StudentID == c.StudentID && x.ComplaintID != c.ComplaintID);

        return View("admin.complaint-show", new { complaint = c, messages, previous });
    }

    [HttpPut("/admin/complaints/{complaint:int}", Name = "admin.complaints.update")]
    public async Task<IActionResult> Update(int complaint, [FromForm] ComplaintUpdateRequest request)
    {
        var c = Db.Complaints.Include(x => x.Listing).Include(x => x.Student).FirstOrDefault(x => x.ComplaintID == complaint) ?? throw new AbortException(404);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var admin = (await AdminUser())!;
        var status = request.Status!;
        c.Status = status;
        c.AdminID = admin.AdminID;
        c.ActionTaken = status == "Under Review" ? null : (request.ActionTaken ?? string.Empty).Trim();
        Db.SaveChanges();

        if (status == "Resolved")
        {
            if (request.RemoveListing && c.Listing != null)
            {
                Service<AdminActions>().RemoveListing(c.Listing, admin, (request.ActionTaken ?? string.Empty).Trim());
            }
            if (request.BlockStudent && c.Student!.AccountStatus == "Active")
            {
                Service<AdminActions>().Block(c.Student);
            }
        }

        TempData["flash"] = "The complaint is now " + status.ToLowerInvariant() + ".";
        return RedirectToRoute("admin.complaints.show", new { complaint = c.ComplaintID });
    }
}
