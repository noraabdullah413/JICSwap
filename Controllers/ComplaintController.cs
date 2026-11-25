 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class ComplaintController : Controller
{
    [HttpGet("/complaints", Name = "complaints.index")]
    public async Task<IActionResult> Index()
    {
        var me = (await Me())!;
        var complaints = PaginatedList<Complaint>.Create(
            Db.Complaints.Include(c => c.Listing).Include(c => c.Student).Where(c => c.ReporterID == me.StudentID).OrderByDescending(c => c.ReportDate), Request, 10);

        return View("student.complaints", new { complaints });
    }

    [HttpGet("/complaints/create", Name = "complaints.create")]
    public async Task<IActionResult> Create([FromQuery] string? listing, [FromQuery] string? type, [FromQuery] string? student)
    {
        var me = (await Me())!;
        Listing? preset = int.TryParse(listing, out var pid) ? Db.Listings.Include(l => l.Seller).FirstOrDefault(l => l.ListingID == pid) : null;
        if (preset != null && preset.SellerID == me.StudentID)
        {
            preset = null;
        }

         
        var ids = Db.Orders.Where(o => o.BuyerID == me.StudentID).SelectMany(o => o.Items.Select(i => i.ListingID))
            .Concat(Db.Messages.Where(m => m.SenderID == me.StudentID || m.ReceiverID == me.StudentID).Select(m => m.ListingID))
            .Concat(Db.Favourites.Where(f => f.StudentID == me.StudentID).Select(f => f.ListingID))
            .ToList();
        if (preset != null) ids.Add(preset.ListingID);
        ids = ids.Distinct().ToList();
        var listings = Db.Listings.Include(l => l.Seller).Where(l => ids.Contains(l.ListingID) && l.SellerID != me.StudentID).OrderBy(l => l.Title).ToList();

        var resolvedType = type == "User" ? "User" : (preset != null ? (preset.IsService() ? "Service" : "Listing") : null);
        var username = !string.IsNullOrEmpty(student) ? student : (resolvedType == "User" && preset != null ? preset.Seller!.Username : null);

        return View("student.complaint-create", new { listings, preset, type = resolvedType, username });
    }

    [HttpPost("/complaints", Name = "complaints.store")]
    public async Task<IActionResult> Store([FromForm] ComplaintRequest request)
    {
        var me = (await Me())!;
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var type = request.ReportType!;
        int studentId;
        int? listingId;
        if (type == "User")
        {
            studentId = Db.Students.Where(s => s.Username == request.ReportedUsername).Select(s => s.StudentID).First();
            listingId = request.ListingID;
        }
        else
        {
            var l = FindOrFail<Listing>(request.ListingID!.Value);
            studentId = l.SellerID;
            listingId = l.ListingID;
        }
        var c = new Complaint
        {
            ReporterID = me.StudentID, StudentID = studentId, ListingID = listingId, ReportType = type, Reason = request.Reason!,
            Description = request.Description!.Trim(), ReportDate = Clock.StartOfSecond(), Status = "Pending",
        };
        Db.Complaints.Add(c);
        Db.SaveChanges();

        TempData["flash"] = "Your complaint " + c.Reference + " was sent. The admin reviews it and you get a notification with the decision.";
        return RedirectToRoute("complaints.index");
    }
}
