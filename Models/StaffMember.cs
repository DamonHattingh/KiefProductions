using System.ComponentModel.DataAnnotations;

namespace KiefProductions.Models;

public class StaffMember
{
    public int Id { get; set; }

    [Required]
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public decimal FlatRate { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    public string? UserId { get; set; }

    public ICollection<EventStaff> EventStaff { get; set; } = new List<EventStaff>();
}
