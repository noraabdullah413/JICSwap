 
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JicSwap.Models;
using JicSwap.Support;
using JicSwap.Support.Middleware;

namespace JicSwap.Controllers;

 
[StudentActive]
public class BrowseController : Controller
{
     



    private IQueryable<Listing> WhereRating(IQueryable<Listing> q, double min)
    {
        var db = Db;
        return q.Where(l => (from r in db.Reviews
                             join oi in db.OrderItems on r.OrderID equals oi.OrderID
                             join l2 in db.Listings on oi.ListingID equals l2.ListingID
                             where l2.SellerID == l.SellerID
                             select (double?)r.Rating).Average() >= min);
    }

    [HttpGet("/browse", Name = "browse")]
    public IActionResult Index()
    {
        var f = Filters();
        var categories = Db.Categories.OrderBy(c => c.CategoryType == "Item" ? 0 : 1).ThenBy(c => c.CategoryID).ToList();
        IQueryable<Listing> Base() => Keyword(Db.Listings.Visible(), (string)f["q"]!);

         
        var counts = new Dictionary<string, object?>
        {
            ["all"] = Base().Count(),
            ["item"] = Base().Count(l => l.ListingType == "Item"),
            ["service"] = Base().Count(l => l.ListingType == "Service"),
            ["cat"] = Base().GroupBy(l => l.CategoryID).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N),
            ["cond"] = Base().Where(l => l.ItemCondition != null).GroupBy(l => l.ItemCondition!).Select(g => new { g.Key, N = g.Count() }).ToDictionary(x => x.Key, x => x.N),
            ["r45"] = WhereRating(Base(), 4.5).Count(),
            ["r40"] = WhereRating(Base(), 4.0).Count(),
        };

        var query = Apply(Base(), f);
        var total = query.Count();
        var sorted = Sort(query, (string)f["sort"]!);
        var results = PaginatedList<Listing>.Create(sorted.Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller), Request, 12);

         
        Dictionary<string, object?>? widen = null;
        if (f["max"] != null && total > 0)
        {
            var wider = new Dictionary<string, object?>(f);
            wider["max"] = Math.Min(20000.0, (double)f["max"]! * 2);
            var more = Apply(Base(), wider).Count() - total;
            if (more > 0)
            {
                widen = new Dictionary<string, object?> { ["min"] = f["min"] ?? 1.0, ["max"] = wider["max"], ["more"] = more };
            }
        }

         
        var popular = new List<Listing>();
        string? popularTitle = null;
        if (total == 0)
        {
            var pq = Db.Listings.Visible();
            var catIds = (List<int>)f["category"]!;
            var names = categories.Where(c => catIds.Contains(c.CategoryID)).Select(c => c.CategoryName).ToList();
            if (names.Count > 0)
            {
                pq = pq.Where(l => catIds.Contains(l.CategoryID));
                popularTitle = "Popular in " + (names.Count > 1 ? string.Join(", ", names.Take(names.Count - 1)) + " and " + names.Last() : names[0]);
            }
            else
            {
                popularTitle = "Popular on JIC Swap";
            }
            popular = pq.Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller).OrderByDescending(l => l.ViewsCount).Take(3).ToList();
            if (popular.Count == 0)
            {
                popularTitle = "Popular on JIC Swap";
                popular = Db.Listings.Visible().Include(l => l.Images).Include(l => l.Category).Include(l => l.Seller).OrderByDescending(l => l.ViewsCount).Take(3).ToList();
            }
        }
        var stats = Student.StatsFor(Db, results.Select(l => l.SellerID).Concat(popular.Select(l => l.SellerID)));

        return View("student.browse", new
        {
            f, categories, counts, results, total, widen, popular, popularTitle, stats,
            chips = Chips(f, categories), filtered = IsFiltered(f),
        });
    }

    private Dictionary<string, object?> Filters()
    {
        var r = Request.Query;
        double? Num(string key) => double.TryParse(r[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : null;
        List<int> Ints(string key) => r[key].Select(x => int.TryParse(x, out var n) ? n : 0).Where(n => n != 0).ToList();

        var q = (r["q"].ToString() ?? string.Empty).Trim();
        if (q.Length > 100) q = q.Substring(0, 100);
        var type = r["type"].ToString();
        var rating = r["rating"].ToString();
        var sort = r["sort"].ToString();

        return new Dictionary<string, object?>
        {
            ["q"] = q,
            ["type"] = type is "item" or "service" ? type : "all",
            ["category"] = Ints("category"),
            ["min"] = Num("min"),
            ["max"] = Num("max"),
            ["condition"] = r["condition"].Where(c => c != null && SwapConfig.Options.Conditions.ContainsKey(c)).Select(c => c!).ToList(),
            ["rating"] = rating is "4.5" or "4.0" ? rating : null,
            ["point"] = Ints("point").Where(p => p is 1 or 2 or 4).ToList(),
            ["sort"] = sort is "featured" or "newest" or "price_asc" or "price_desc" or "rating" ? sort : "featured",
        };
    }

    private IQueryable<Listing> Keyword(IQueryable<Listing> q, string kw)
    {
        if (kw != string.Empty)
        {
            var like = "%" + kw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
            q = q.Where(l => EF.Functions.Like(l.Title, like) || EF.Functions.Like(l.Description, like));
        }
        return q;
    }

    private IQueryable<Listing> Apply(IQueryable<Listing> q, Dictionary<string, object?> f)
    {
        if ((string)f["type"]! != "all")
        {
            var t = char.ToUpperInvariant(((string)f["type"]!)[0]) + ((string)f["type"]!).Substring(1);    
            q = q.Where(l => l.ListingType == t);
        }
        var cats = (List<int>)f["category"]!;
        if (cats.Count > 0) q = q.Where(l => cats.Contains(l.CategoryID));
        if (f["min"] != null) { var min = (decimal)(double)f["min"]!; q = q.Where(l => l.Price >= min); }
        if (f["max"] != null) { var max = (decimal)(double)f["max"]!; q = q.Where(l => l.Price <= max); }
        var cond = (List<string>)f["condition"]!;
        if (cond.Count > 0) q = q.Where(l => cond.Contains(l.ItemCondition!));
        if (f["rating"] != null)
        {
            q = WhereRating(q, double.Parse((string)f["rating"]!, CultureInfo.InvariantCulture));
        }
        var points = (List<int>)f["point"]!;
        if (points.Count > 0)
        {
            var mask = points.Sum();
             
            q = q.Where(l => l.ListingType == "Service" || (((int)(l.MeetingPoints ?? 0)) & mask) > 0);
        }
        return q;
    }

    private IQueryable<Listing> Sort(IQueryable<Listing> q, string sort)
    {
        var db = Db;
        var o = q.FeaturedFirst();
        o = sort switch
        {
            "price_asc" => o.ThenBy(l => l.Price),
            "price_desc" => o.ThenByDescending(l => l.Price),
            "rating" => o.ThenByDescending(l => (from r in db.Reviews
                                                 join oi in db.OrderItems on r.OrderID equals oi.OrderID
                                                 join l2 in db.Listings on oi.ListingID equals l2.ListingID
                                                 where l2.SellerID == l.SellerID
                                                 select (double?)r.Rating).Average() ?? 0),    
            _ => o,
        };
        return o.ThenByDescending(l => l.PostedDate).ThenByDescending(l => l.ListingID);
    }

    private static bool IsFiltered(Dictionary<string, object?> f)
    {
        return (string)f["q"]! != string.Empty || (string)f["type"]! != "all" || ((List<int>)f["category"]!).Count > 0 || f["min"] != null || f["max"] != null
            || ((List<string>)f["condition"]!).Count > 0 || f["rating"] != null || ((List<int>)f["point"]!).Count > 0;
    }

     
    private List<object?[]> Chips(Dictionary<string, object?> f, List<Category> categories)
    {
         
        var req = Request.Query.Where(k => k.Key != "page").ToDictionary(k => k.Key, k => (object?)(k.Value.Count > 1 ? k.Value.ToArray() : (object?)k.Value.ToString()));
        string Without(string key, object? value = null)
        {
            var q = new Dictionary<string, object?>(req);
            if (value == null)
            {
                q.Remove(key);
            }
            else
            {
                var current = Request.Query[key].Where(v => v != value.ToString()).ToArray();    
                q[key] = current;
            }
            return Url.RouteUrl("browse", q)!;
        }

        var chips = new List<object?[]>();
        if ((string)f["type"]! != "all")
        {
            chips.Add(new object?[] { "Type: " + ((string)f["type"]! == "item" ? "Items only" : "Services only"), Without("type") });
        }
        foreach (var id in (List<int>)f["category"]!)
        {
            var c = categories.FirstOrDefault(x => x.CategoryID == id);
            if (c != null) chips.Add(new object?[] { "Category: " + c.CategoryName, Without("category", id) });
        }
        if (f["min"] != null || f["max"] != null)
        {
            var q = new Dictionary<string, object?>(req);
            q.Remove("min"); q.Remove("max");
            string Max(double m) => Helpers.NumberFormat(m, m % 1 != 0 ? 2 : 0);
            var label = f["min"] != null && f["max"] != null ? Helpers.Sar((double)f["min"]!) + " – " + Max((double)f["max"]!)
                : (f["min"] != null ? "from " + Helpers.Sar((double)f["min"]!) : "up to " + Helpers.Sar((double)f["max"]!));
            chips.Add(new object?[] { "Price: " + label, Url.RouteUrl("browse", q)! });
        }
        foreach (var c in (List<string>)f["condition"]!)
        {
            chips.Add(new object?[] { "Condition: " + Helpers.ConditionLabel(c), Without("condition", c) });
        }
        if (f["rating"] != null)
        {
            chips.Add(new object?[] { "Seller rating: " + f["rating"] + " and above", Without("rating") });
        }
        foreach (var b in (List<int>)f["point"]!)
        {
            chips.Add(new object?[] { "Meeting point: " + Helpers.PointName(b), Without("point", b) });
        }
        return chips;
    }
}
