 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("PaymentID")]
    public int PaymentID { get; set; }

     
     
    [Column("OrderID")] public int? OrderID { get; set; }
    [Column("ListingID")] public int? ListingID { get; set; }
    [Column("PaymentType")] [MaxLength(15)] public string PaymentType { get; set; } = string.Empty;
    [Column("PaymentMethod")] [MaxLength(20)] public string PaymentMethod { get; set; } = string.Empty;

     
    [Column("Amount", TypeName = "decimal(10,2)")] public decimal Amount { get; set; }
    [Column("ReferenceNumber")] [MaxLength(30)] public string? ReferenceNumber { get; set; }
    [Column("PaymentDate", TypeName = "datetime2")] public DateTime PaymentDate { get; set; } = DateTime.Now;

    [Column("Status")] [MaxLength(10)] public string Status { get; set; } = "Pending";

     
    public Order? Order { get; set; }
     
    public Listing? Listing { get; set; }

     
    public bool IsOnline() => PaymentMethod != "Cash on delivery";

     
    [NotMapped]
    public string StatusPill => Status switch
    {
        "Paid" => "pill-paid",
        "Pending" => "pill-pending",
        "Failed" => "pill-failed",
        _ => "pill-cancelled",
    };

     
    [NotMapped]
    public string Summary => Helpers.PayShort(PaymentMethod) + " · " + Status;

     
    [NotMapped]
    public DateTime HeldUntil => PaymentDate.AddMinutes(SwapConfig.Options.PaymentHoldMinutes);
}
