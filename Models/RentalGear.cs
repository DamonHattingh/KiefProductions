using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class RentalGear
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public decimal MarketPrice { get; set; }
    public decimal OurCost { get; set; }
    public string? Supplier { get; set; }

    public int ProductTypeId { get; set; }
    public ProductType ProductType { get; set; } = null!;

    public ICollection<GearBooking> GearBookings { get; set; } = new List<GearBooking>();
}
