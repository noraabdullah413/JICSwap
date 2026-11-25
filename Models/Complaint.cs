 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JicSwap.Models;

[Table("complaints")]
public class Complaint
{
    [Key]
    [Column("ComplaintID")]
    public int ComplaintID { get; set; }

     
     
    [Column("ReporterID")] public int ReporterID { get; set; }
    [Column("StudentID")] public int StudentID { get; set; }
    [Column("ListingID")] public int? ListingID { get; set; }
    [Column("AdminID")] public int? AdminID { get; set; }
    [Column("ReportType")] [MaxLength(10)] public string ReportType { get; set; } = string.Empty;
    [Column("Reason")] [MaxLength(100)] public string Reason { get; set; } = string.Empty;
    [Column("Description")] [MaxLength(1000)] public string Description { get; set; } = string.Empty;

     
    [Column("ReportDate", TypeName = "datetime2")] public DateTime ReportDate { get; set; } = DateTime.Now;

    [Column("Status")] [MaxLength(15)] public string Status { get; set; } = "Pending";
    [Column("ActionTaken")] [MaxLength(300)] public string? ActionTaken { get; set; }

     
    public Student? Reporter { get; set; }
     
    public Student? Student { get; set; }
     
    public Listing? Listing { get; set; }
     
    public Admin? Admin { get; set; }

     
    [NotMapped]
    public string StatusPill => Status switch
    {
        "Pending" => "pill-pending",
        "Under Review" => "pill-confirmed",
        "Resolved" => "pill-completed",
        "Dismissed" => "pill-cancelled",
        _ => "pill-cancelled",
    };

     
    [NotMapped]
    public string Reference => string.Format("CMP-{0}-{1:000}", ReportDate.ToString("yyyy"), ComplaintID);
}
