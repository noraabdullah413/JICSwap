 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JicSwap.Models;

[Table("announcements")]
public class Announcement
{
    [Key]
    [Column("AnnouncementID")]
    public int AnnouncementID { get; set; }

     
    [Column("AdminID")] public int AdminID { get; set; }
    [Column("Title")] [MaxLength(150)] public string Title { get; set; } = string.Empty;
    [Column("Message")] [MaxLength(2000)] public string Message { get; set; } = string.Empty;

     
    [Column("PostedDate", TypeName = "datetime2")] public DateTime PostedDate { get; set; } = DateTime.Now;

    [Column("Status")] [MaxLength(10)] public string Status { get; set; } = "Active";

     
    [ForeignKey(nameof(AdminID))] public Admin? Admin { get; set; }

     
    [NotMapped]
    public string Reference => string.Format("ANN-{0}-{1:000}", PostedDate.ToString("yyyy"), AnnouncementID);

     
    [NotMapped]
    public string StatusPill => Status == "Active" ? "pill-new" : "pill-cancelled";
}
