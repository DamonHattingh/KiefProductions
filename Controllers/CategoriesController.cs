using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;
    public CategoriesController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var cats = await _context.Categories
            .Include(c => c.ProductTypes)
            .ThenInclude(pt => pt.Gears)
            .Include(c => c.ProductTypes)
            .ThenInclude(pt => pt.RentalGears)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(cats);
    }

    public IActionResult Create() => View(new Category());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid) return View(category);
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Category '{category.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var cat = await _context.Categories
            .Include(c => c.ProductTypes)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cat == null) return NotFound();
        return View(cat);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return NotFound();
        if (!ModelState.IsValid) return View(category);
        _context.Update(category);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _context.Categories
            .Include(c => c.ProductTypes)
                .ThenInclude(pt => pt.Gears)
            .Include(c => c.ProductTypes)
                .ThenInclude(pt => pt.RentalGears)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cat == null) return NotFound();

        // Check if category has any product types
        if (cat.ProductTypes.Any())
        {
            TempData["Error"] = $"Cannot delete category '{cat.Name}' because it has product types associated with it. Please delete all product types first.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(cat);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProductType(int id, int categoryId)
    {
        var pt = await _context.ProductTypes
            .Include(pt => pt.Gears)
            .Include(pt => pt.RentalGears)
            .FirstOrDefaultAsync(pt => pt.Id == id);

        if (pt == null) return NotFound();

        // Check if product type has any gears or rental gears
        if (pt.Gears.Any() || pt.RentalGears.Any())
        {
            TempData["Error"] = $"Cannot delete product type '{pt.Name}' because it has associated items. Please delete all items first.";
            return RedirectToAction(nameof(Edit), new { id = categoryId });
        }

        _context.ProductTypes.Remove(pt);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Edit), new { id = categoryId });
    }

    // ── Product Types ──────────────────────────────────────
    public async Task<IActionResult> AddProductType(int id)
    {
        var cat = await _context.Categories.FindAsync(id);
        if (cat == null) return NotFound();
        ViewBag.CategoryName = cat.Name;
        return View(new ProductType { CategoryId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProductType(ProductType pt)
    {
        ModelState.Remove("Category");
        ModelState.Remove("Id");
        if (!ModelState.IsValid)
        {
            ViewBag.CategoryName = (await _context.Categories.FindAsync(pt.CategoryId))?.Name;
            return View(pt);
        }
        pt.Id = 0; // ensure EF treats this as a new insert
        _context.ProductTypes.Add(pt);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Type '{pt.Name}' added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> EditProductType(int id)
    {
        var pt = await _context.ProductTypes
            .Include(pt => pt.Category)
            .FirstOrDefaultAsync(pt => pt.Id == id);
        if (pt == null) return NotFound();
        ViewBag.CategoryName = pt.Category.Name;
        return View(pt);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProductType(int id, ProductType pt)
    {
        if (id != pt.Id) return NotFound();
        ModelState.Remove("Category");
        ModelState.Remove("Gears");
        ModelState.Remove("RentalGears");
        if (!ModelState.IsValid)
        {
            ViewBag.CategoryName = (await _context.Categories.FindAsync(pt.CategoryId))?.Name;
            return View(pt);
        }
        _context.Update(pt);
        await _context.SaveChangesAsync();
        TempData["Success"] = $"Type '{pt.Name}' updated.";
        return RedirectToAction(nameof(Edit), new { id = pt.CategoryId });
    }
}