 
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Exceptions;
using JicSwap.Support.Middleware;
using JicSwap.Support.Services;
using JicSwap.ViewModels;

namespace JicSwap.Controllers;

 
[Authorize(AuthenticationSchemes = "student")]
[StudentActive]
public class ListingWizardController : Controller
{
    private ListingWizard Wizard => Service<ListingWizard>();

    [HttpGet("/listings/create", Name = "listings.create")]
    public IActionResult Type()
    {
        var d = Wizard.Get();
         
        return View("student.wizard-type", new { selected = Input.Old(TempData, "ListingType", d.Type ?? "Item"), draft = d });
    }

    [HttpPost("/listings/create", Name = "listings.create.type")]
    public IActionResult ChooseType([FromForm] ListingTypeRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        var type = request.ListingType!;
        var d = Wizard.Get();
        if (d.Type != type || d.EditId != null)
        {
            Wizard.Start(type);
        }

        return RedirectToRoute(type == "Item" ? "listings.create.item" : "listings.create.service");
    }

    [HttpGet("/listings/create/item", Name = "listings.create.item")]
    public IActionResult ItemForm()
    {
        var d = EnsureDraft("Item");
        return View("student.wizard-item", new
        {
            d, categories = Db.Categories.Where(c => c.CategoryType == "Item").OrderBy(c => c.CategoryName).ToList(), ranges = PriceRanges(),
        });
    }

    [HttpPost("/listings/create/item", Name = "listings.create.item.store")]
    public IActionResult ItemStore([FromForm] ItemDetailsRequest request)
    {
        EnsureDraft("Item");
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        Wizard.Merge(new Dictionary<string, object?>
        {
            ["Title"] = request.Title, ["CategoryID"] = request.CategoryID, ["Price"] = ParsePrice(request.Price),
            ["ItemCondition"] = request.ItemCondition, ["Description"] = request.Description,
            ["meeting_points"] = request.MeetingPoints, ["details_ok"] = true,
        });

        return RedirectToRoute("listings.create.photos");
    }

    [HttpGet("/listings/create/photos", Name = "listings.create.photos")]
    public IActionResult Photos()
    {
        var d = EnsureDraft("Item");
        if (!d.DetailsOk)
        {
            return RedirectToRoute("listings.create.item");
        }

        return View("student.wizard-photos", new { d, category = Db.Categories.Find(d.CategoryID) });
    }

    [HttpPost("/listings/create/photos", Name = "listings.create.photos.store")]
    public async Task<IActionResult> PhotosStore([FromForm] PhotosRequest request)
    {
        EnsureDraft("Item");
        var failedValidation = ValidateRequest(request);
        if (failedValidation != null) return failedValidation;

        var max = SwapConfig.Options.MaxPhotos;
        var failed = new List<Dictionary<string, object?>>();
        var done = new List<DraftFile>();
        foreach (var file in request.Photos ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
        {
            var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            var mime = file.ContentType;
            if (Wizard.Get().Photos.Count >= max)
            {
                failed.Add(new() { ["name"] = file.FileName, ["size"] = file.Length, ["msg"] = "A listing can have at most " + max + " photos." });
            }
            else if (file.Length > SwapConfig.Options.MaxPhotoKb * 1024L)
            {
                failed.Add(new() { ["name"] = file.FileName, ["size"] = file.Length, ["msg"] = "This file is larger than the 5 MB limit." });
            }
            else if (!(ext is "jpg" or "jpeg" or "png") || !(mime is "image/jpeg" or "image/png"))
            {
                failed.Add(new() { ["name"] = file.FileName, ["size"] = file.Length, ["msg"] = "Only JPG or PNG photos can be uploaded." });
            }
            else
            {
                done.Add(await Wizard.Keep(file, "photos"));
            }
        }
        if (done.Count > 0)
        {
            TempData["uploaded_now"] = JsonSerializer.Serialize(done);    
        }
        if (failed.Count > 0)
        {
            TempData["failed_uploads"] = JsonSerializer.Serialize(failed);
        }

        if (request.Continue)
        {
            if (Wizard.Get().Photos.Count == 0)
            {
                FlashErrors(new Dictionary<string, string> { ["photos"] = "You cannot publish without a photo." });
                return RedirectToRoute("listings.create.photos");
            }
            return RedirectToRoute("listings.create.preview");
        }

        return RedirectToRoute("listings.create.photos");
    }

    [HttpPost("/listings/create/photos/remove", Name = "listings.create.photos.remove")]
    public IActionResult PhotoRemove([FromForm] string? id)
    {
        Wizard.Remove("photos", id ?? string.Empty);
        return Back();
    }

    [HttpPost("/listings/create/proof/remove", Name = "listings.create.proof.remove")]
    public IActionResult ProofRemove([FromForm] string? id)
    {
        Wizard.Remove("proof_files", id ?? string.Empty);
        return Back();
    }

     
    [HttpGet("/listings/create/tmp/{id}", Name = "listings.create.tmp")]
    public IActionResult Tmp(string id)
    {
        var d = Wizard.Get();
        foreach (var f in d.Photos.Concat(d.ProofFiles))
        {
            if (f.Id == id && !string.IsNullOrEmpty(f.Tmp))
            {
                var path = Service<UploadsDisk>().Path_(f.Tmp);
                new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);
                return PhysicalFile(path, contentType ?? "application/octet-stream");    
            }
        }
        throw new AbortException(404);
    }

    [HttpGet("/listings/create/service", Name = "listings.create.service")]
    public IActionResult ServiceForm()
    {
        var d = EnsureDraft("Service");
         
        var keys = Input.Errors(TempData).Keys.ToList();
        var proofOnly = d.DetailsOk && keys.Count > 0 && keys.All(k => k.StartsWith("proof_", StringComparison.Ordinal));

        return View(proofOnly ? "student.wizard-service-blocked" : "student.wizard-service", new
        {
            d, categories = Db.Categories.Where(c => c.CategoryType == "Service").OrderBy(c => c.CategoryName).ToList(),
        });
    }

    [HttpPost("/listings/create/service", Name = "listings.create.service.store")]
    public async Task<IActionResult> ServiceStore([FromForm] ServiceDetailsRequest request)
    {
        EnsureDraft("Service");
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        foreach (var file in request.ProofFiles ?? new List<Microsoft.AspNetCore.Http.IFormFile>())
        {
            await Wizard.Keep(file, "proof_files");
        }
         
        Wizard.Merge(new Dictionary<string, object?>
        {
            ["Title"] = request.Title, ["CategoryID"] = request.CategoryID, ["Price"] = ParsePrice(request.Price), ["PriceUnit"] = request.PriceUnit,
            ["DeliveredAt"] = request.DeliveredAt, ["days"] = request.Days, ["time_from"] = request.TimeFrom, ["time_to"] = request.TimeTo,
            ["Description"] = request.Description, ["proof_type"] = request.ProofType, ["details_ok"] = true,
        });

        return RedirectToRoute("listings.create.preview");
    }

    [HttpGet("/listings/create/preview", Name = "listings.create.preview")]
    public IActionResult Preview()
    {
        var d = Wizard.Get();
        if (string.IsNullOrEmpty(d.Type) || !d.DetailsOk)
        {
            return RedirectToRoute("listings.create");
        }
        if (d.Type == "Item" && d.Photos.Count == 0)
        {
            FlashErrors(new Dictionary<string, string> { ["photos"] = "You cannot publish without a photo." });
            return RedirectToRoute("listings.create.photos");
        }
        if (d.Type == "Service" && d.ProofFiles.Count == 0)
        {
            return RedirectToRoute("listings.create.service");
        }

        return View("student.wizard-preview", new { d, category = Db.Categories.Find(d.CategoryID) });
    }

    [HttpPost("/listings/create/publish", Name = "listings.create.publish")]
    public async Task<IActionResult> Publish()
    {
        var d = Wizard.Get();
        if (!d.DetailsOk)
        {
            return RedirectToRoute("listings.create");
        }
        var editing = d.EditId != null;
        var listing = await Wizard.Publish((await Me())!);

        if (editing)
        {
            TempData["flash"] = "Your changes are saved." + (listing.WaitingProof ? " The admin checks the new proof before the service is shown again." : "");
            return RedirectToRoute("my-listings.show", new { listing = listing.ListingID });
        }
        return RedirectToRoute("my-listings.published", new { listing = listing.ListingID });
    }

     
    [HttpPost("/listings/create/draft", Name = "listings.create.draft")]
    public IActionResult Draft()
    {
        var d = Wizard.Get();
        if (string.IsNullOrEmpty(d.Type))
        {
            return RedirectToRoute("listings.create");
        }
        var fields = new[] { "Title", "CategoryID", "Price", "ItemCondition", "Description", "meeting_points", "PriceUnit", "DeliveredAt", "days", "time_from", "time_to", "proof_type" };
        var vals = new Dictionary<string, object?>();
        foreach (var f in fields)
        {
            if (!Request.Form.ContainsKey(f)) continue;
            var raw = Request.Form[f];
            vals[f] = f switch
            {
                "meeting_points" => raw.Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n != 0).ToList(),
                "days" => raw.Select(x => x ?? string.Empty).ToList(),
                "CategoryID" => int.TryParse(raw.ToString(), out var cid) ? cid : null,
                "Price" => ParsePrice(raw.ToString()),
                _ => Limit(raw.ToString().Trim(), 1500),    
            };
        }
        Wizard.Merge(vals);

        TempData["flash"] = "Your draft is saved. Drafts are not visible to other students - open “Post a listing” to finish it.";
        return RedirectToRoute("my-listings.index");
    }

    [HttpGet("/listings/create/cancel", Name = "listings.create.cancel")]
    public IActionResult Cancel()
    {
        Wizard.Discard();
        return RedirectToRoute("my-listings.index");
    }

    [HttpGet("/my-listings/{listing:int}/edit", Name = "my-listings.edit")]
    public async Task<IActionResult> Edit(int listing)
    {
        var me = (await Me())!;
        var l = Db.Listings.Include(x => x.Images).Include(x => x.Category).FirstOrDefault(x => x.ListingID == listing) ?? throw new AbortException(404);
        AbortException.Unless(l.SellerID == me.StudentID, 403);
        if (l.Status is "Reserved" or "Sold")
        {
            TempData["error"] = "A reserved or sold listing cannot be edited.";
            return RedirectToRoute("my-listings.show", new { listing = l.ListingID });
        }
        Wizard.Start(l.ListingType, l);

        return RedirectToRoute(l.IsItem() ? "listings.create.item" : "listings.create.service");
    }

    private ListingDraft EnsureDraft(string type)
    {
        var d = Wizard.Get();
        if (d.Type != type)
        {
            d = Wizard.Start(type);
        }
        return d;
    }

    private static decimal? ParsePrice(string? v) =>
        decimal.TryParse((v ?? string.Empty).Replace(",", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) ? p : null;

    private static string Limit(string s, int n) => s.Length <= n ? s : s.Substring(0, n);    

     
    private Dictionary<int, string> PriceRanges()
    {
        return Db.Listings.Where(l => (l.Status == "Sold" || l.Status == "Available") && l.ListingType == "Item")
            .GroupBy(l => l.CategoryID).Select(g => new { CategoryID = g.Key, Mn = g.Min(l => l.Price), Mx = g.Max(l => l.Price), N = g.Count() })
            .ToList().Where(r => r.N >= 3)
            .ToDictionary(r => r.CategoryID, r => "Similar listings on JIC Swap sell for SAR " + Helpers.NumberFormat((double)r.Mn) + " – " + Helpers.NumberFormat((double)r.Mx) + ".");
    }
}
