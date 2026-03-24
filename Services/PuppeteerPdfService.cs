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

        var pdf = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "15mm",
                Bottom = "15mm",
                Left = "15mm",
                Right = "15mm"
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