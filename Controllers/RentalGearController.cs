using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class RentalGearController : Controller
{
    private readonly ApplicationDbContext _context;
    public RentalGearController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _context.RentalGears
            .Include(g => g.ProductType).ThenInclude(pt => pt.Category)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(g => g.Name.Contains(search) || (g.Supplier != null && g.Supplier.Contains(search)));

        if (categoryId.HasValue)
            query = query.Where(g => g.ProductType.CategoryId == categoryId);

        ViewBag.Search = search;
        ViewBag.CategoryId = categoryId;
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

        return View(await query.OrderBy(g => g.ProductType.Category.Name).ThenBy(g => g.Name).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        await PopulateViewBag();
        return View(new RentalGear());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RentalGear gear)
    {
        ModelState.Remove("ProductType");
        if (!ModelState.IsValid) { await PopulateViewBag(); return View(gear); }
        _context.RentalGears.Add(gear);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Rental gear '{gear.Name}' added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var gear = await _context.RentalGears.FindAsync(id);
        if (gear == null) return NotFound();
        await PopulateViewBag(gear.ProductTypeId);
        return View(gear);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RentalGear gear)
    {
        if (id != gear.Id) return NotFound();
        ModelState.Remove("ProductType");
        if (!ModelState.IsValid) { await PopulateViewBag(gear.ProductTypeId); return View(gear); }
        _context.Update(gear);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Rental gear updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var gear = await _context.RentalGears.FindAsync(id);
        if (gear != null) { _context.RentalGears.Remove(gear); await _context.SaveChangesAsync(); TempData["Success"] = "Rental gear deleted."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetProductTypes(int categoryId)
    {
        var types = await _context.ProductTypes
            .Where(pt => pt.CategoryId == categoryId)
            .OrderBy(pt => pt.Name)
            .Select(pt => new { pt.Id, pt.Name })
            .ToListAsync();
        return Json(types);
    }

    private async Task PopulateViewBag(int? selectedTypeId = null)
    {
        ViewBag.Categories = await _context.Categories.Include(c => c.ProductTypes).OrderBy(c => c.Name).ToListAsync();
        ViewBag.ProductTypes = new SelectList(
            await _context.ProductTypes.Include(pt => pt.Category).OrderBy(pt => pt.Category.Name).ThenBy(pt => pt.Name).ToListAsync(),
            "Id", "Name", selectedTypeId);
    }
}
