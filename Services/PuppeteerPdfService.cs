using Microsoft.AspNetCore.Components;
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

    private string GetChromiumPath() => Path.Combine(_env.WebRootPath, "chromium");
    private async Task EnsureBrowserAsync()
    {
        if (_browserDownloaded) return;

        await _lock.WaitAsync();
        try
        {
            if (!_browserDownloaded)
            {
                var chromiumPath = GetChromiumPath();
                Directory.CreateDirectory(chromiumPath);

                var fetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = chromiumPath
                });
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
        try
        {
            await EnsureBrowserAsync();

            var fetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Path = GetChromiumPath()
            });
            var installedBrowser = fetcher.GetInstalledBrowsers().FirstOrDefault();
            var executablePath = installedBrowser?.GetExecutablePath()
                ?? throw new InvalidOperationException("Chromium not found in expected folder.");

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executablePath,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            await using var page = await browser.NewPageAsync();
            await page.SetContentAsync(html, new PuppeteerSharp.NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle2 }
            });

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
        catch (Exception ex)
        {
            var errorPath = Path.Combine(_env.WebRootPath, "pdf-error.txt");
            await File.WriteAllTextAsync(errorPath, ex.ToString());
            throw;
        }
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