 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("orders")]
public class Order
{
    [Key]
    [Column("OrderID")]
    public int OrderID { get; set; }

     
     
    [Column("BuyerID")] public int BuyerID { get; set; }

     
    [Column("OrderDate", TypeName = "datetime2")] public DateTime OrderDate { get; set; } = DateTime.Now;

     
    [Column("Amount", TypeName = "decimal(10,2)")] public decimal Amount { get; set; }
    [Column("CommissionFee", TypeName = "decimal(10,2)")] public decimal CommissionFee { get; set; }

    [Column("MeetingPoint")] [MaxLength(30)] public string MeetingPoint { get; set; } = string.Empty;
    [Column("Status")] [MaxLength(10)] public string Status { get; set; } = "Pending";
    [Column("CompletedDate", TypeName = "datetime2")] public DateTime? CompletedDate { get; set; }
    [Column("MeetingTime", TypeName = "datetime2")] public DateTime MeetingTime { get; set; }
    [Column("DeliveredDate", TypeName = "datetime2")] public DateTime? DeliveredDate { get; set; }

     
    public Student? Buyer { get; set; }
     
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

     



    [NotMapped] public OrderItem? Item => Items.OrderBy(i => i.OrderItemID).FirstOrDefault();

     
    public Payment? Payment { get; set; }
     
    public Review? Review { get; set; }
     
    public ReturnRequest? ReturnRequest { get; set; }

     
    [NotMapped] public Listing? Listing => Item?.Listing;

     
    [NotMapped]
    public string Reference => string.Format("PUR-{0}-{1:0000}", OrderDate.ToString("yyyy"), OrderID);

     
    [NotMapped]
    public double Earnings => Math.Round((double)Amount - (double)CommissionFee, 2, MidpointRounding.AwayFromZero);

     
    [NotMapped]
    public string StatusPill => Status switch
    {
        "Pending" => "pill-pending",
        "Confirmed" => "pill-confirmed",
        "Completed" => "pill-completed",
        "Cancelled" => "pill-cancelled",
        _ => "pill-cancelled",
    };

     
    [NotMapped]
    public string SellerLabel => Status switch
    {
        "Pending" => "Waiting",
        "Confirmed" => "Accepted",
        "Completed" => "Completed",
        "Cancelled" => "Rejected",
        _ => Status,
    };

     
    [NotMapped] public DateTime Deadline => OrderDate.AddHours(SwapConfig.Options.RequestHours);

     
    [NotMapped]
    public int HoursLeft => Math.Max(0, (int)Math.Floor((int)(Deadline - DateTime.Now).TotalMinutes / 60.0));

     
    [NotMapped]
    public DateTime? ReturnDeadline => CompletedDate?.AddDays(SwapConfig.Options.ReturnDays);

     
    public bool CanRequestReturn()
    {
        return Status == "Completed" && ReturnDeadline.HasValue && DateTime.Now <= ReturnDeadline.Value && ReturnRequest == null;
    }
}
