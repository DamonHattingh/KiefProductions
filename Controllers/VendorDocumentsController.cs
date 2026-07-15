using KiefProductions.Data;
using KiefProductions.Models;
using KiefProductions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KiefProductions.Controllers;

[Authorize]
public class VendorDocumentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly VendorDocumentExtractionService _extractionService;

    private const string UploadsFolder = "uploads/vendor-docs";
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };

    public VendorDocumentsController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        VendorDocumentExtractionService extractionService)
    {
        _context = context;
        _env = env;
        _extractionService = extractionService;
    }

    public async Task<IActionResult> Index(int? eventId, VendorDocumentType? type, string? search)
    {
        var query = _context.VendorDocuments.Include(v => v.Event).AsQueryable();

        if (eventId.HasValue)
            query = query.Where(v => v.EventId == eventId);

        if (type.HasValue)
            query = query.Where(v => v.DocumentType == type);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(v => v.VendorName.Contains(search) ||
                                      (v.DocumentNumber != null && v.DocumentNumber.Contains(search)));

        ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
        ViewBag.EventId = eventId;
        ViewBag.Type = type;
        ViewBag.Search = search;

        return View(await query.OrderByDescending(v => v.UploadedDate).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
        return View(new VendorDocumentUploadViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, int? eventId)
    {
        ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();

        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please choose a file to upload.");
            return View("Create", new VendorDocumentUploadViewModel { EventId = eventId });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            ModelState.AddModelError(string.Empty, "Only PDF, JPG and PNG files are supported.");
            return View("Create", new VendorDocumentUploadViewModel { EventId = eventId });
        }

        var uploadsPath = Path.Combine(_env.WebRootPath, UploadsFolder);
        Directory.CreateDirectory(uploadsPath);

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsPath, storedFileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var extraction = _extractionService.Extract(fullPath, file.ContentType);

        var model = new VendorDocumentUploadViewModel
        {
            FileName = storedFileName,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            EventId = eventId,
            VendorName = extraction.VendorName ?? string.Empty,
            DocumentNumber = extraction.DocumentNumber,
            DocumentType = extraction.DocumentType ?? VendorDocumentType.Invoice,
            DocumentDate = extraction.DocumentDate,
            Amount = extraction.Amount,
            IsUploaded = true,
            TextExtracted = extraction.TextExtracted
        };

        return View("Create", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(VendorDocumentUploadViewModel model)
    {
        ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
        model.IsUploaded = true;

        if (string.IsNullOrWhiteSpace(model.FileName) || string.IsNullOrWhiteSpace(model.OriginalFileName))
        {
            ModelState.AddModelError(string.Empty, "Something went wrong with the upload — please upload the file again.");
            model.IsUploaded = false;
            return View("Create", model);
        }

        if (string.IsNullOrWhiteSpace(model.VendorName))
        {
            ModelState.AddModelError(nameof(model.VendorName), "Vendor/company name is required.");
            return View("Create", model);
        }

        var document = new VendorDocument
        {
            VendorName = model.VendorName.Trim(),
            DocumentNumber = string.IsNullOrWhiteSpace(model.DocumentNumber) ? null : model.DocumentNumber.Trim(),
            DocumentType = model.DocumentType,
            DocumentDate = model.DocumentDate,
            Amount = model.Amount,
            Notes = model.Notes,
            FileName = model.FileName,
            OriginalFileName = model.OriginalFileName,
            ContentType = model.ContentType,
            EventId = model.EventId,
            UploadedDate = DateTime.Now
        };

        _context.VendorDocuments.Add(document);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"{document.DocumentType} from '{document.VendorName}' saved.";
        return RedirectToAction(nameof(Details), new { id = document.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var document = await _context.VendorDocuments.Include(v => v.Event).FirstOrDefaultAsync(v => v.Id == id);
        if (document == null) return NotFound();
        return View(document);
    }

    public async Task<IActionResult> Edit(int id)
    {
        var document = await _context.VendorDocuments.FindAsync(id);
        if (document == null) return NotFound();

        ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
        return View(document);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VendorDocument model)
    {
        if (id != model.Id) return NotFound();

        var document = await _context.VendorDocuments.FindAsync(id);
        if (document == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.VendorName))
            ModelState.AddModelError(nameof(model.VendorName), "Vendor/company name is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Events = await _context.Events.OrderByDescending(e => e.EventDate).ToListAsync();
            model.FileName = document.FileName;
            model.OriginalFileName = document.OriginalFileName;
            model.ContentType = document.ContentType;
            model.UploadedDate = document.UploadedDate;
            return View(model);
        }

        document.VendorName = model.VendorName.Trim();
        document.DocumentNumber = string.IsNullOrWhiteSpace(model.DocumentNumber) ? null : model.DocumentNumber.Trim();
        document.DocumentType = model.DocumentType;
        document.DocumentDate = model.DocumentDate;
        document.Amount = model.Amount;
        document.Notes = model.Notes;
        document.EventId = model.EventId;

        await _context.SaveChangesAsync();
        TempData["Success"] = "Document updated.";
        return RedirectToAction(nameof(Details), new { id = document.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var document = await _context.VendorDocuments.FindAsync(id);
        if (document == null) return NotFound();

        var filePath = Path.Combine(_env.WebRootPath, UploadsFolder, document.FileName);
        if (System.IO.File.Exists(filePath))
            System.IO.File.Delete(filePath);

        _context.VendorDocuments.Remove(document);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Document from '{document.VendorName}' deleted.";
        return RedirectToAction(nameof(Index));
    }
}

// ── View Models ───────────────────────────────────────────────
public class VendorDocumentUploadViewModel
{
    public int? Id { get; set; }
    public string? FileName { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public bool IsUploaded { get; set; }
    public bool TextExtracted { get; set; }

    public string VendorName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public VendorDocumentType DocumentType { get; set; } = VendorDocumentType.Invoice;
    public DateTime? DocumentDate { get; set; }
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
    public int? EventId { get; set; }
}
