 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JicSwap.Models;

[Table("messages")]
public class Message
{
    [Key]
    [Column("MessageID")]
    public int MessageID { get; set; }

     
    [Column("SenderID")] public int SenderID { get; set; }
    [Column("ReceiverID")] public int ReceiverID { get; set; }
    [Column("ListingID")] public int ListingID { get; set; }
    [Column("Content")] [MaxLength(1000)] public string Content { get; set; } = string.Empty;

     
    [Column("SentDate", TypeName = "datetime2")] public DateTime SentDate { get; set; } = DateTime.Now;
    [Column("IsRead")] public bool IsRead { get; set; }

     
    public Student? Sender { get; set; }
     
    public Student? Receiver { get; set; }
     
    public Listing? Listing { get; set; }
}

 
public static class MessageQueryExtensions
{
     
    public static IQueryable<Message> Thread(this IQueryable<Message> q, int listingId, int a, int b)
    {
        return q.Where(m => m.ListingID == listingId
            && ((m.SenderID == a && m.ReceiverID == b) || (m.SenderID == b && m.ReceiverID == a)));
    }
}
