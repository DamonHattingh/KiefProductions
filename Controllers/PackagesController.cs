using KiefProductions.Data;
using KiefProductions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class PackagesController : Controller
{
    private readonly ApplicationDbContext _context;
    public PackagesController(ApplicationDbContext context) => _context = context;

    public async Task<IActionResult> Index()
    {
        var packages = await _context.Packages
            .Include(p => p.PackageItems).ThenInclude(pi => pi.Gear).ThenInclude(g => g.ProductType)
            .OrderBy(p => p.Name)
            .ToListAsync();
        return View(packages);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Gears = await _context.Gears
            .Include(g => g.ProductType).ThenInclude(pt => pt.Category)
            .OrderBy(g => g.ProductType.Category.Name).ThenBy(g => g.Name)
            .ToListAsync();
        return View(new Package());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Package package, List<int>? gearIds, List<int>? gearQuantities, List<string>? customDescriptions)
    {
        ModelState.Remove("PackageItems");
        if (!ModelState.IsValid)
        {
            ViewBag.Gears = await _context.Gears.Include(g => g.ProductType).ThenInclude(pt => pt.Category).ToListAsync();
            return View(package);
        }

        _context.Packages.Add(package);
        await _context.SaveChangesAsync();

        if (gearIds != null)
        {
            for (int i = 0; i < gearIds.Count; i++)
            {
                if (gearIds[i] > 0)
                {
                    _context.PackageItems.Add(new PackageItem
                    {
                        PackageId = package.Id,
                        GearId = gearIds[i],
                        Quantity = gearQuantities != null && i < gearQuantities.Count ? gearQuantities[i] : 1
                    });
                }
            }
        }

        if (customDescriptions != null)
        {
            foreach (var desc in customDescriptions.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                _context.PackageItems.Add(new PackageItem { PackageId = package.Id, CustomDescription = desc, Quantity = 1 });
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = $"Package '{package.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var package = await _context.Packages
            .Include(p => p.PackageItems).ThenInclude(pi => pi.Gear)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (package == null) return NotFound();

        ViewBag.Gears = await _context.Gears
            .Include(g => g.ProductType).ThenInclude(pt => pt.Category)
            .OrderBy(g => g.ProductType.Category.Name).ThenBy(g => g.Name)
            .ToListAsync();
        return View(package);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Package package)
    {
        if (id != package.Id) return NotFound();
        ModelState.Remove("PackageItems");
        if (!ModelState.IsValid)
        {
            ViewBag.Gears = await _context.Gears.Include(g => g.ProductType).ThenInclude(pt => pt.Category).ToListAsync();
            return View(package);
        }

        var existing = await _context.Packages.Include(p => p.PackageItems).FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null) return NotFound();

        existing.Name = package.Name;
        existing.BasePrice = package.BasePrice;
        existing.IsActive = package.IsActive;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Package updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var package = await _context.Packages.Include(p => p.PackageItems).FirstOrDefaultAsync(p => p.Id == id);
        if (package != null)
        {
            _context.PackageItems.RemoveRange(package.PackageItems);
            _context.Packages.Remove(package);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Package deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int itemId, int packageId)
    {
        var item = await _context.PackageItems.FindAsync(itemId);
        if (item != null) { _context.PackageItems.Remove(item); await _context.SaveChangesAsync(); }
        return RedirectToAction(nameof(Edit), new { id = packageId });
    }
}
