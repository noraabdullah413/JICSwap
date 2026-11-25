 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using JicSwap.Data;
using JicSwap.Support;

namespace JicSwap.Models;

 
[Table("students")]
public class Student
{
    [Key]
    [Column("StudentID")]
    public int StudentID { get; set; }

     
     
    [Column("FirstName")] [MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Column("LastName")] [MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [Column("Username")] [MaxLength(20)] public string Username { get; set; } = string.Empty;
    [Column("Email")] [MaxLength(150)] public string Email { get; set; } = string.Empty;

     
    [JsonIgnore]
    [Column("Password")] [MaxLength(255)] public string Password { get; set; } = string.Empty;

    [Column("PhoneNumber")] [MaxLength(12)] public string PhoneNumber { get; set; } = string.Empty;
    [Column("Major")] [MaxLength(100)] public string Major { get; set; } = string.Empty;
    [Column("ProfileImage")] [MaxLength(500)] public string? ProfileImage { get; set; }
    [Column("AccountStatus")] [MaxLength(10)] public string AccountStatus { get; set; } = "Active";

     
    [Column("JoinDate", TypeName = "date")] public DateTime JoinDate { get; set; } = DateTime.Today;

     
    [NotMapped] public int ListingsCount { get; set; }

     
    [NotMapped] public int ComplaintsCount { get; set; }

     
    public string GetAuthPassword() => Password;

     
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
     
    public ICollection<Order> Orders { get; set; } = new List<Order>();
     
    public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();

     

     
    [NotMapped] public string Initials => Helpers.Initials(FirstName, LastName);

     
    [NotMapped]
    public string ShortName => FirstName + " " + (LastName ?? string.Empty).Substring(0, Math.Min(1, (LastName ?? string.Empty).Length)) + ".";

     
    [NotMapped] public string FullName => FirstName + " " + LastName;

     
    [NotMapped] public string? AvatarUrl => Helpers.MediaUrl(ProfileImage);

    public bool IsBlocked() => AccountStatus == "Blocked";

     

     
    public record SellerStatsRow(double? Rating, int Reviews, int Sales)
    {
         
        public object? this[string key] => key switch { "rating" => Rating, "reviews" => Reviews, "sales" => Sales, _ => null };
    }

     
    public class RatingRow
    {
        public Review R { get; set; } = null!;
        public OrderItem Oi { get; set; } = null!;
        public Listing L { get; set; } = null!;
    }

     
    public static IQueryable<RatingRow> RatingQuery(AppDbContext db)
    {
        return from r in db.Reviews
               join oi in db.OrderItems on r.OrderID equals oi.OrderID
               join l in db.Listings on oi.ListingID equals l.ListingID
               select new RatingRow { R = r, Oi = oi, L = l };
    }

     
    public SellerStatsRow SellerStats(AppDbContext db)
    {
        var all = StatsFor(db, new[] { StudentID });
        return all.TryGetValue(StudentID, out var row) ? row : new SellerStatsRow(null, 0, 0);
    }

     
    public static Dictionary<int, SellerStatsRow> StatsFor(AppDbContext db, IEnumerable<int> ids)
    {
        var list = ids.Where(i => i != 0).Distinct().ToList();
        var outp = new Dictionary<int, SellerStatsRow>();
        foreach (var id in list) outp[id] = new SellerStatsRow(null, 0, 0);
        if (list.Count == 0) return outp;

         
        var rows = (from t in RatingQuery(db)
                    where list.Contains(t.L.SellerID)
                    group t by t.L.SellerID into g
                    select new { sid = g.Key, avg = g.Average(x => (double)x.R.Rating), cnt = g.Count() }).ToList();
        foreach (var r in rows)
            outp[r.sid] = new SellerStatsRow(Math.Round(r.avg, 1, MidpointRounding.AwayFromZero), r.cnt, outp[r.sid].Sales);

         
        var sales = (from o in db.Orders
                     join oi in db.OrderItems on o.OrderID equals oi.OrderID
                     join l in db.Listings on oi.ListingID equals l.ListingID
                     where o.Status == "Completed" && list.Contains(l.SellerID)
                     group l by l.SellerID into g
                     select new { sid = g.Key, cnt = g.Count() }).ToList();
        foreach (var s in sales)
            outp[s.sid] = outp[s.sid] with { Sales = s.cnt };

        return outp;
    }
}
