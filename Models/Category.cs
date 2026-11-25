 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("categories")]
public class Category
{
    [Key]
    [Column("CategoryID")]
    public int CategoryID { get; set; }

     
    [Column("AdminID")] public int AdminID { get; set; }
    [Column("CategoryName")] [MaxLength(60)] public string CategoryName { get; set; } = string.Empty;
    [Column("Description")] [MaxLength(300)] public string? Description { get; set; }
    [Column("CategoryType")] [MaxLength(10)] public string CategoryType { get; set; } = "Item";

     
    [NotMapped] public int ListingsCount { get; set; }

     
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();

     
    [ForeignKey(nameof(AdminID))] public Admin? Admin { get; set; }

     
    [NotMapped]
    public string Icon => Helpers.CategoryIcon(CategoryName);
}
