 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JicSwap.Models;

[Table("order_items")]
public class OrderItem
{
    [Key]
    [Column("OrderItemID")]
    public int OrderItemID { get; set; }

     
    [Column("OrderID")] public int OrderID { get; set; }
    [Column("ListingID")] public int ListingID { get; set; }

     
    [Column("Price", TypeName = "decimal(10,2)")] public decimal Price { get; set; }

     
    public Order? Order { get; set; }
     
    public Listing? Listing { get; set; }
}
