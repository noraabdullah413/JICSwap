 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Services;
using JicSwap.Support.Middleware;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class ProfileController : Controller
{
    [HttpGet("/profile", Name = "profile.show")]
    public async Task<IActionResult> Show()
    {
        var me = (await Me())!;
        var stats = me.SellerStats(Db);
        var tiles = new Dictionary<string, object?>
        {
            ["active"] = Db.Listings.Count(l => l.SellerID == me.StudentID && l.Status == "Available"),
            ["sales"] = stats.Sales,
            ["purchases"] = Db.Orders.Count(o => o.BuyerID == me.StudentID && o.Status == "Completed"),
            ["rating"] = stats.Rating,
        };
        var reviews = Review.ForSeller(Db, me.StudentID).Take(5).ToList();

        return View("student.profile", new { me, stats, tiles, reviews });
    }

    [HttpPut("/profile", Name = "profile.update")]
    public async Task<IActionResult> Update([FromForm] ProfileRequest request)
    {
        var me = (await Me())!;
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var disk = Service<UploadsDisk>();
        me.FirstName = request.FirstName!;
        me.LastName = request.LastName!;
        me.Username = request.Username!;
        me.PhoneNumber = request.PhoneNormalised!;
        me.Major = request.Major!;
        var file = request.ProfileImage;
        if (file != null)
        {
            if (!string.IsNullOrEmpty(me.ProfileImage) && !me.ProfileImage.StartsWith("http", StringComparison.Ordinal))
            {
                disk.Delete(me.ProfileImage);
            }
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            me.ProfileImage = await disk.StoreAs(file, "profiles", me.StudentID + "-" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ext);
        }
        if (request.RemovePhoto && file == null)
        {
            if (!string.IsNullOrEmpty(me.ProfileImage) && !me.ProfileImage.StartsWith("http", StringComparison.Ordinal))
            {
                disk.Delete(me.ProfileImage);
            }
            me.ProfileImage = null;
        }
        Db.SaveChanges();

        TempData["flash"] = "Your profile is saved.";
        return RedirectToRoute("profile.show");
    }

    [HttpPut("/profile/password", Name = "profile.password")]
    public async Task<IActionResult> Password([FromForm] PasswordChangeRequest request)
    {
        var me = (await Me())!;
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        me.Password = Hash.Make(request.Password!);
        Db.SaveChanges();

        TempData["flash"] = "Your password has been changed.";
        return RedirectToRoute("profile.show");
    }

     
    [HttpGet("/u/{username}", Name = "profile.public")]
    public async Task<IActionResult> PublicProfile(string username)
    {
        var me = (await Me())!;
        var student = Db.Students.FirstOrDefault(s => s.Username == username && s.AccountStatus == "Active")
            ?? throw new Support.Exceptions.AbortException(404);    
        var stats = student.SellerStats(Db);
        var listings = Db.Listings.Visible().Where(l => l.SellerID == student.StudentID)
            .Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller)
            .FeaturedFirst().ThenByDescending(l => l.PostedDate).ToList();
        var reviews = Review.ForSeller(Db, student.StudentID).Take(5).ToList();
        var mine = student.StudentID == me.StudentID;

        return View("student.profile-public", new { student, stats, listings, reviews, mine });
    }
}
