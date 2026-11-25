 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Services;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class StudentController : Controller
{
    [HttpGet("/admin/students", Name = "admin.students.index")]
    public IActionResult Index([FromQuery] string? status, [FromQuery] string? q)
    {
        status = status is "Active" or "Blocked" ? status : "All";
        q = (q ?? string.Empty).Trim();
        var query = Db.Students.AsQueryable();
        if (status != "All")
        {
            query = query.Where(s => s.AccountStatus == status);
        }
        if (q != string.Empty)
        {
            var like = "%" + q + "%";
            query = query.Where(s => EF.Functions.Like(s.Username, like) || EF.Functions.Like(s.FirstName, like)
                || EF.Functions.Like(s.LastName, like) || EF.Functions.Like(s.Email, like));
        }
        var students = PaginatedList<Student>.Create(query.OrderBy(s => s.FirstName), Request, 15);
         
        foreach (var s in students)
        {
            s.ListingsCount = Db.Listings.Count(l => l.SellerID == s.StudentID);
            s.ComplaintsCount = Db.Complaints.Count(c => c.StudentID == s.StudentID);
        }
        var counts = new Dictionary<string, int>
        {
            ["All"] = Db.Students.Count(),
            ["Active"] = Db.Students.Count(s => s.AccountStatus == "Active"),
            ["Blocked"] = Db.Students.Count(s => s.AccountStatus == "Blocked"),
        };

        return View("admin.students", new { students, counts, status, q });
    }

    [HttpGet("/admin/students/{student:int}", Name = "admin.students.show")]
    public IActionResult Show(int student)
    {
        var s = FindOrFail<Student>(student);
        var stats = s.SellerStats(Db);
        var listings = Db.Listings.Where(l => l.SellerID == s.StudentID).Include(l => l.Images).Include(l => l.Category)
            .OrderByDescending(l => l.PostedDate).ToList();
        foreach (var l in listings) l.OrderItemsCount = Db.OrderItems.Count(i => i.ListingID == l.ListingID);
        var orders = Db.Orders.Where(o => o.BuyerID == s.StudentID).Include(o => o.Payment)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Seller)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Category)
            .Include(o => o.Items).ThenInclude(i => i.Listing).ThenInclude(l => l!.Images)
            .OrderByDescending(o => o.OrderDate).Take(10).ToList();
        var complaints = Db.Complaints.Where(c => c.StudentID == s.StudentID).Include(c => c.Reporter).OrderByDescending(c => c.ReportDate).ToList();

        return View("admin.student-show", new { student = s, stats, listings, orders, complaints });
    }

    [HttpPost("/admin/students/{student:int}/block", Name = "admin.students.block")]
    public IActionResult Block(int student)
    {
        var s = FindOrFail<Student>(student);
        if (s.AccountStatus != "Blocked")
        {
            Service<AdminActions>().Block(s);
        }

        TempData["flash"] = "@" + s.Username + " is blocked. Their available listings are inactive and their open orders were cancelled.";
        return Back();
    }

    [HttpPost("/admin/students/{student:int}/unblock", Name = "admin.students.unblock")]
    public IActionResult Unblock(int student)
    {
        var s = FindOrFail<Student>(student);
        Service<AdminActions>().Unblock(s);

        TempData["flash"] = "@" + s.Username + " can sign in again. Their listings stay inactive until they activate them.";
        return Back();
    }
}
