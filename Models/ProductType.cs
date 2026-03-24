using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class ProductType
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public ICollection<Gear> Gears { get; set; } = new List<Gear>();
    public ICollection<RentalGear> RentalGears { get; set; } = new List<RentalGear>();
}
