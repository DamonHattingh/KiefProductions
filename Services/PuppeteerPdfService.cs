using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace KiefProductions.Services;

public class PuppeteerPdfService
{
    private readonly IWebHostEnvironment _env;
    private static bool _browserDownloaded = false;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public PuppeteerPdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browserDownloaded) return;

        await _lock.WaitAsync();
        try
        {
            if (!_browserDownloaded)
            {
                var fetcher = new BrowserFetcher();
                await fetcher.DownloadAsync();
                _browserDownloaded = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<byte[]> GeneratePdfFromHtmlAsync(string html)
    {
        await EnsureBrowserAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
        });

        // 👇 Add this
        await page.EvaluateExpressionAsync(@"
    const footer = document.querySelector('.page-footer');
if (footer) {
    const pageHeight = 1123;
    const footerTop = footer.getBoundingClientRect().top;
    const pageNumber = Math.ceil(footerTop / pageHeight);
    const currentPageBottom = pageNumber * pageHeight;
    const gap = currentPageBottom - footer.getBoundingClientRect().bottom;
    const offset = pageNumber === 1 ? 45 : 170;
    if (gap > 10 && gap < pageHeight * 0.8) {
        footer.style.marginTop = (gap - offset) + 'px';
    }
}
");

        var pdf = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            }
        });
        return pdf;
    }

    public string GetLogoBase64()
    {
        var logoPath = Path.Combine(_env.WebRootPath, "images", "Kief_Square_Logo.png");
        if (!File.Exists(logoPath)) return string.Empty;
        var bytes = File.ReadAllBytes(logoPath);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    public string GetBankLogoBase64()
    {
        var logoPath = Path.Combine(_env.WebRootPath, "images", "bank-logo.png");
        if (!File.Exists(logoPath)) return string.Empty;
        var bytes = File.ReadAllBytes(logoPath);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}