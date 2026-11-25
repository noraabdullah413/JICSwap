 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JicSwap.Models;

[Table("favourites")]
public class Favourite
{
    [Key]
    [Column("FavouriteID")]
    public int FavouriteID { get; set; }

     
    [Column("StudentID")] public int StudentID { get; set; }
    [Column("ListingID")] public int ListingID { get; set; }

     
    [Column("DateAdded", TypeName = "datetime2")] public DateTime DateAdded { get; set; } = DateTime.Now;

     
    public Student? Student { get; set; }
     
    public Listing? Listing { get; set; }
}
