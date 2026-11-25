 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using JicSwap.Data;

namespace JicSwap.Models;

[Table("reviews")]
public class Review
{
    [Key]
    [Column("ReviewID")]
    public int ReviewID { get; set; }

     
    [Column("OrderID")] public int OrderID { get; set; }

     
    [Column("Rating")] public int Rating { get; set; }
    [Column("Comment")] [MaxLength(500)] public string? Comment { get; set; }
    [Column("ReviewDate", TypeName = "datetime2")] public DateTime ReviewDate { get; set; } = DateTime.Now;

     
    public Order? Order { get; set; }

     



    public static IQueryable<Review> ForSeller(AppDbContext db, int sellerId)
    {
         
        return (from r in db.Reviews
                join oi in db.OrderItems on r.OrderID equals oi.OrderID
                join l in db.Listings on oi.ListingID equals l.ListingID
                where l.SellerID == sellerId
                orderby r.ReviewDate descending
                select r)
            .Include(r => r.Order).ThenInclude(o => o!.Buyer);
    }
}
