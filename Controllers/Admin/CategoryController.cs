 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.ViewModels.Admin;

namespace JicSwap.Controllers.Admin;

 
[Authorize(AuthenticationSchemes = "admin")]
public class CategoryController : Controller
{
     
    private List<Category> AllCategories()
    {
        var list = Db.Categories.Include(c => c.Admin).OrderBy(c => c.CategoryType == "Item" ? 0 : 1).ThenBy(c => c.CategoryName).ToList();
        foreach (var c in list) c.ListingsCount = Db.Listings.Count(l => l.CategoryID == c.CategoryID);
        return list;
    }

    [HttpGet("/admin/categories", Name = "admin.categories.index")]
    public IActionResult Index()
    {
        return View("admin.categories", new { categories = AllCategories(), edit = (Category?)null });
    }

    [HttpGet("/admin/categories/{category:int}/edit", Name = "admin.categories.edit")]
    public IActionResult Edit(int category)
    {
        return View("admin.categories", new { categories = AllCategories(), edit = FindOrFail<Category>(category) });
    }

    [HttpPost("/admin/categories", Name = "admin.categories.store")]
    public async Task<IActionResult> Store([FromForm] CategoryRequest request)
    {
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        Db.Categories.Add(new Category
        {
            CategoryName = request.CategoryName!, CategoryType = request.CategoryType!, Description = request.Description,
            AdminID = (await AdminUser())!.AdminID,    
        });
        Db.SaveChanges();

        TempData["flash"] = "The category was added.";
        return RedirectToRoute("admin.categories.index");
    }

    [HttpPut("/admin/categories/{category:int}", Name = "admin.categories.update")]
    public IActionResult Update(int category, [FromForm] CategoryRequest request)
    {
        var c = FindOrFail<Category>(category);
        var failed = ValidateRequest(request);
        if (failed != null) return failed;

        if (request.CategoryType != c.CategoryType && Db.Listings.Any(l => l.CategoryID == c.CategoryID))
        {
            return BackWithErrors(new Dictionary<string, string> { ["CategoryType"] = "The type cannot change while the category has listings." });
        }
        c.CategoryName = request.CategoryName!;
        c.CategoryType = request.CategoryType!;
        c.Description = request.Description;
        Db.SaveChanges();

        TempData["flash"] = "The category was saved.";
        return RedirectToRoute("admin.categories.index");
    }

    [HttpDelete("/admin/categories/{category:int}", Name = "admin.categories.destroy")]
    public IActionResult Destroy(int category)
    {
        var c = FindOrFail<Category>(category);
        var n = Db.Listings.Count(l => l.CategoryID == c.CategoryID);
        if (n > 0)
        {
            TempData["error"] = "This category still has " + n + " " + (n == 1 ? "listing" : "listings") + ". Move or remove them first.";
            return Back();
        }
        Db.Categories.Remove(c);
        Db.SaveChanges();

        TempData["flash"] = "The category was deleted.";
        return RedirectToRoute("admin.categories.index");
    }
}
