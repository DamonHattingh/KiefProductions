using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Package
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    public ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
}
