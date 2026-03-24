using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class GearController : Controller
{
    private readonly ApplicationDbContext _context;
    public GearController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var query = _context.Gears
            .Include(g => g.ProductType).ThenInclude(pt => pt.Category)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(g => g.Name.Contains(search) || (g.Description != null && g.Description.Contains(search)));

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
        return View(new Gear());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Gear gear)
    {
        ModelState.Remove("ProductType");
        if (!ModelState.IsValid) { await PopulateViewBag(); return View(gear); }

        _context.Gears.Add(gear);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Gear '{gear.Name}' added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var gear = await _context.Gears.FindAsync(id);
        if (gear == null) return NotFound();
        await PopulateViewBag(gear.ProductTypeId);
        return View(gear);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Gear gear)
    {
        if (id != gear.Id) return NotFound();
        ModelState.Remove("ProductType");
        if (!ModelState.IsValid) { await PopulateViewBag(gear.ProductTypeId); return View(gear); }

        _context.Update(gear);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Gear updated.";
        return RedirectToAction(nameof(Index));
    }

    // AJAX: Get product types by category
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
        var categories = await _context.Categories.Include(c => c.ProductTypes).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Categories = categories;
        ViewBag.ProductTypes = new SelectList(
            await _context.ProductTypes.Include(pt => pt.Category).OrderBy(pt => pt.Category.Name).ThenBy(pt => pt.Name).ToListAsync(),
            "Id", "Name", selectedTypeId);
    }
}
