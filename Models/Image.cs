 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JicSwap.Support;

namespace JicSwap.Models;

[Table("images")]
public class Image
{
    [Key]
    [Column("ImageID")]
    public int ImageID { get; set; }

     
    [Column("ListingID")] public int ListingID { get; set; }

     
    [Column("Image")] [MaxLength(500)] public string ImagePath { get; set; } = string.Empty;

     
    public Listing? Listing { get; set; }

     
    [NotMapped]
    public string? Url => Helpers.MediaUrl(ImagePath);
}
