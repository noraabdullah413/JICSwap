 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using JicSwap.Support;

namespace JicSwap.Models;

 
[Table("admins")]
public class Admin
{
    [Key]
    [Column("AdminID")]
    public int AdminID { get; set; }

     
    [Column("FullName")] [MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [Column("Email")] [MaxLength(150)] public string Email { get; set; } = string.Empty;

     
    [JsonIgnore]
    [Column("Password")] [MaxLength(255)] public string Password { get; set; } = string.Empty;

    [Column("PhoneNumber")] [MaxLength(12)] public string PhoneNumber { get; set; } = string.Empty;

     

     
    public string GetAuthPassword() => Password;

     
    [NotMapped]
    public string Initials
    {
        get
        {
            var parts = System.Text.RegularExpressions.Regex.Split((FullName ?? string.Empty).Trim(), @"\s+");
            var first = parts.Length > 0 ? parts[0] : string.Empty;
            var last = parts.Length > 0 ? parts[^1] : string.Empty;
            return Helpers.Initials(first, string.IsNullOrEmpty(last) ? string.Empty : last);
        }
    }

     
    [NotMapped]
    public string ShortName
    {
        get
        {
            var parts = System.Text.RegularExpressions.Regex.Split((FullName ?? string.Empty).Trim(), @"\s+");
            var first = parts.Length > 0 ? parts[0] : string.Empty;
            return first + (parts.Length > 1 ? " " + parts[^1].Substring(0, Math.Min(1, parts[^1].Length)) + "." : string.Empty);
        }
    }
}
