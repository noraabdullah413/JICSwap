 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("return_requests")]
public class ReturnRequest
{
    [Key]
    [Column("ReturnID")]
    public int ReturnID { get; set; }

     
    [Column("OrderID")] public int OrderID { get; set; }
    [Column("AdminID")] public int? AdminID { get; set; }
    [Column("Reason")] [MaxLength(500)] public string Reason { get; set; } = string.Empty;
    [Column("EvidenceImage")] [MaxLength(500)] public string EvidenceImage { get; set; } = string.Empty;

     
    [Column("RefundAmount", TypeName = "decimal(10,2)")] public decimal? RefundAmount { get; set; }
    [Column("RequestDate", TypeName = "datetime2")] public DateTime RequestDate { get; set; } = DateTime.Now;

    [Column("Status")] [MaxLength(10)] public string Status { get; set; } = "Pending";

     
    public Order? Order { get; set; }
     
    public Admin? Admin { get; set; }

     
    [NotMapped]
    public string? EvidenceUrl => Helpers.MediaUrl(EvidenceImage);

     
    [NotMapped]
    public string StatusPill => Status switch
    {
        "Pending" => "pill-pending",
        "Approved" => "pill-completed",
        "Rejected" => "pill-failed",
        _ => "pill-cancelled",
    };

     
    [NotMapped]
    public string Reference => string.Format("RET-{0}-{1:000}", RequestDate.ToString("yyyy"), ReturnID);
}
