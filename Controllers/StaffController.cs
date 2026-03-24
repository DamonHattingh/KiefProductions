using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;
    public StaffController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        return View(await _context.StaffMembers.OrderBy(s => s.FullName).ToListAsync());
    }

    public IActionResult Create() => View(new StaffMember());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffMember staff)
    {
        if (!ModelState.IsValid) return View(staff);
        _context.StaffMembers.Add(staff);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Staff member '{staff.FullName}' added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var staff = await _context.StaffMembers.FindAsync(id);
        if (staff == null) return NotFound();
        return View(staff);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StaffMember staff)
    {
        if (id != staff.Id) return NotFound();
        if (!ModelState.IsValid) return View(staff);
        _context.Update(staff);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Staff member updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var staff = await _context.StaffMembers.FindAsync(id);
        if (staff != null)
        {
            staff.IsActive = false;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Staff member deactivated.";
        }
        return RedirectToAction(nameof(Index));
    }
}
