 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("listings")]
public class Listing
{
    [Key]
    [Column("ListingID")]
    public int ListingID { get; set; }

     
     
     
    [Column("SellerID")] public int SellerID { get; set; }
    [Column("CategoryID")] public int CategoryID { get; set; }
    [Column("ListingType")] [MaxLength(10)] public string ListingType { get; set; } = "Item";
    [Column("Title")] [MaxLength(100)] public string Title { get; set; } = string.Empty;
    [Column("Description")] [MaxLength(1500)] public string Description { get; set; } = string.Empty;

     
    [Column("Price", TypeName = "decimal(10,2)")] public decimal Price { get; set; }
    [Column("ItemCondition")] [MaxLength(10)] public string? ItemCondition { get; set; }
    [Column("Availability")] [MaxLength(120)] public string? Availability { get; set; }
    [Column("Status")] [MaxLength(10)] public string Status { get; set; } = "Available";

     
    [Column("IsFeatured")] public bool IsFeatured { get; set; }
    [Column("FeaturedUntil", TypeName = "date")] public DateTime? FeaturedUntil { get; set; }

     
    [Column("PostedDate", TypeName = "datetime2")] public DateTime PostedDate { get; set; } = DateTime.Now;

     
    [Column("proof_of_experience")] [MaxLength(500)] public string? ProofOfExperience { get; set; }
    [Column("PriceUnit")] [MaxLength(10)] public string? PriceUnit { get; set; }
    [Column("DeliveredAt")] [MaxLength(100)] public string? DeliveredAt { get; set; }

     
    [Column("MeetingPoints")] public byte? MeetingPoints { get; set; }

     
    [Column("ViewsCount")] public int ViewsCount { get; set; }

     
    [Column("ProofVerifiedAt", TypeName = "datetime2")] public DateTime? ProofVerifiedAt { get; set; }

     
    [NotMapped] public int OrderItemsCount { get; set; }

     

     
    public Student? Seller { get; set; }
     
    public Category? Category { get; set; }
     
     
    public ICollection<Image> Images { get; set; } = new List<Image>();
     
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
     
    public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();
     
    public ICollection<Message> Messages { get; set; } = new List<Message>();
     
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
     
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

     




    [NotMapped]
    public IEnumerable<Payment> FeaturePayments => Payments.Where(p => p.PaymentType == "Featured fee");

     

     
    [NotMapped]
    public string Reference => string.Format("LST-{0}-{1:0000}", PostedDate.ToString("yyyy"), ListingID);

    public bool IsService() => ListingType == "Service";

    public bool IsItem() => ListingType == "Item";

     
    [NotMapped]
    public bool FeaturedNow => IsFeatured && FeaturedUntil.HasValue && FeaturedUntil.Value.Date >= DateTime.Today;

     
    [NotMapped]
    public bool WaitingProof => IsService() && ProofVerifiedAt == null && Status == "Available";

     
    [NotMapped]
    public string? CoverUrl => Images.OrderBy(i => i.ImageID).FirstOrDefault()?.Url;

     
    [NotMapped]
    public string UnitLabel => PriceUnit != null && SwapConfig.Options.PriceUnits.TryGetValue(PriceUnit, out var v) ? v : string.Empty;

     
    [NotMapped]
    public string ConditionLabel => Helpers.ConditionLabel(ItemCondition);

     
    [NotMapped]
    public string SubLine => (Category?.CategoryName ?? string.Empty) + " · " + (IsService() ? "Service" : ConditionLabel);

     
    [NotMapped]
    public int[] PointBits
    {
        get
        {
            if (IsService()) return new[] { 1, 2, 4 };
            return new[] { 1, 2, 4 }.Where(b => (((int)(MeetingPoints ?? 0)) & b) == b).ToArray();
        }
    }

     
    [NotMapped]
    public string[] PointNames => PointBits.Select(b => Helpers.PointName(b)).ToArray();

     
    [NotMapped]
    public string StatusPill
    {
        get
        {
            if (WaitingProof) return "pill-pending";
            return Status switch { "Available" => "pill-completed", "Reserved" => "pill-reserved", _ => "pill-cancelled" };
        }
    }

     
    [NotMapped]
    public string StatusLabel => WaitingProof ? "Waiting for proof check" : Status;

     
    [NotMapped]
    public double Earnings => Helpers.Earnings((double)Price);

     
    [NotMapped]
    public double Commission => Helpers.Commission((double)Price);

     
    [NotMapped]
    public string? ProofUrl => Helpers.MediaUrl(ProofOfExperience);

     



    public bool IsReferenced()
    {
        return OrderItems.Any() || FeaturePayments.Any() || Messages.Any() || Favourites.Any() || Complaints.Any();
    }

     
    public bool IsReferenced(JicSwap.Data.AppDbContext db)
    {
        return db.OrderItems.Any(x => x.ListingID == ListingID)
            || db.Payments.Any(x => x.ListingID == ListingID && x.PaymentType == "Featured fee")
            || db.Messages.Any(x => x.ListingID == ListingID)
            || db.Favourites.Any(x => x.ListingID == ListingID)
            || db.Complaints.Any(x => x.ListingID == ListingID);
    }
}

 
public static class ListingQueryExtensions
{
     
    public static IQueryable<Listing> Visible(this IQueryable<Listing> q)
    {
        return q.Where(l => l.Status == "Available")
            .Where(l => l.ListingType == "Item" || l.ProofVerifiedAt != null)
            .Where(l => l.Seller!.AccountStatus == "Active");    
    }

     
    public static IOrderedQueryable<Listing> FeaturedFirst(this IQueryable<Listing> q)
    {
         
        var today = DateTime.Today;
        return q.OrderBy(l => (l.IsFeatured && l.FeaturedUntil >= today) ? 0 : 1);
    }
}
