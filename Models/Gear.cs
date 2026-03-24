using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class Gear
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public decimal MarketPrice { get; set; }

    public int ProductTypeId { get; set; }
    public ProductType ProductType { get; set; } = null!;

    public ICollection<PackageItem> PackageItems { get; set; } = new List<PackageItem>();
    public ICollection<GearBooking> GearBookings { get; set; } = new List<GearBooking>();
}
