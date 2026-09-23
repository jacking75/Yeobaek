using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Browser;
using Yeobaek.Data;
using Yeobaek.Reader;

namespace Yeobaek.Archive;

public sealed record ArchiveResult(ArchivedArticle Article, int ImagesTotal, int ImagesSaved);

/// <summary>
/// 오프라인 보관. 추출한 본문과 이미지를 archive\&lt;폴더&gt;\ 에 저장하고 검색 색인에 넣는다.
/// 보관본은 가상 호스트(https://archive.yeobaek.example/&lt;폴더&gt;/index.html)로 열며 인터넷 없이도 보인다.
/// 원문이 지워지거나 바뀌어도 남는다. 폴더 이름은 원문 주소로 정해져 다시 보관하면 덮어쓴다.
/// </summary>
public sealed class ArchiveService
{
    /// <summary>실제 사이트와 겹치지 않도록 RFC 6761 예약 도메인(.example)을 쓴다.</summary>
    public const string VirtualHost = "archive.yeobaek.example";

    private const long MaxImageBytes = 15L * 1024 * 1024;
    private const string TempFolderPrefix = ".tmp-";
    private static readonly string UrlPrefix = $"https://{VirtualHost}/";
    private static readonly TimeSpan SaveTimeout = TimeSpan.FromSeconds(90);

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private readonly ArticleStore _articles;
    private readonly SettingsFile _settings;
    private readonly RequestBlocker _blocker;

    public ArchiveService(ArticleStore articles, SettingsFile settings, RequestBlocker blocker)
    {
        _articles = articles;
        _settings = settings;
        _blocker = blocker;
        DeleteLeftoverTempFolders();
    }

    public static bool IsArchiveUrl(string? url)
        => url is not null && url.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase);

    public static string PageUrlFor(ArchivedArticle article) => $"{UrlPrefix}{article.Folder}/index.html";

    /// <summary>탭마다 가상 호스트를 매핑한다. 다른 사이트는 이 파일들을 읽을 수 없다(Deny).</summary>
    public void Attach(CoreWebView2 core)
        => core.SetVirtualHostNameToFolderMapping(VirtualHost, AppPaths.Archive, CoreWebView2HostResourceAccessKind.Deny);

    /// <summary>보관본 주소면 원문 주소를, 아니면 null 을 돌려준다.</summary>
    public string? GetSourceUrl(string? url)
    {
        if (!IsArchiveUrl(url)) return null;
        var folder = url![UrlPrefix.Length..].Split('/', '?', '#')[0];
        return folder.Length == 0 ? null : _articles.FindByFolder(folder)?.Url;
    }

    /// <summary>
    /// 글을 보관한다. 파일은 임시 폴더에 다 쓴 뒤 한 번에 바꿔 넣어, 실패해도 이전 보관본이 망가지지 않는다.
    /// 내려받지 못한 이미지는 원래 주소로 남긴다(인터넷이 있을 때만 보임).
    /// </summary>
    public async Task<ArchiveResult> SaveAsync(ExtractedArticle article, string userAgent)
    {
        var content = article.Content;
        var folder = ArticleExtractor.KeyFor(content.SourceUrl);
        var tempDir = Path.Combine(AppPaths.Archive, TempFolderPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            var imagesDir = Directory.CreateDirectory(Path.Combine(tempDir, "images")).FullName;
            using var timeout = new CancellationTokenSource(SaveTimeout);
            var localized = await ArchiveImages.LocalizeAsync(content.ContentHtml,
                (url, index) => DownloadImageAsync(url, index, imagesDir, content.SourceUrl, userAgent, timeout.Token));

            var page = ReaderPage.Build(content with { ContentHtml = localized.Html }, _settings.Current.Reader, DateTime.Now);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "index.html"), page, Encoding.UTF8);

            var sizeBytes = new DirectoryInfo(tempDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            ReplaceDirectory(tempDir, Path.Combine(AppPaths.Archive, folder));

            var saved = _articles.Save(content.SourceUrl, content.Title, content.Byline,
                content.SiteName ?? HostName.FromUrl(content.SourceUrl), folder, sizeBytes, article.PlainText);
            return new ArchiveResult(saved, localized.Total, localized.Saved);
        }
        finally
        {
            TryDeleteDirectory(tempDir);   // 성공했다면 이미 옮겨져 없다
        }
    }

    public void Delete(ArchivedArticle article)
    {
        _articles.Delete(article.Id);
        TryDeleteDirectory(Path.Combine(AppPaths.Archive, article.Folder));
    }

    private async Task<string?> DownloadImageAsync(
        string url, int index, string imagesDir, string pageUrl, string userAgent, CancellationToken cancellation)
    {
        if (_blocker.ShouldBlock(url)) return null;   // 광고 이미지는 보관하지 않는다
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/*,*/*;q=0.8");
            request.Headers.Referrer = new Uri(pageUrl);   // 원문을 볼 때와 같게 보내야 핫링크 차단을 피한다

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if (!response.IsSuccessStatusCode) return null;
            if (response.Content.Headers.ContentLength > MaxImageBytes) return null;

            var extension = ImageExtension(response.Content.Headers.ContentType?.MediaType, url);
            if (extension is null) return null;

            var fileName = $"{index:000}{extension}";
            var path = Path.Combine(imagesDir, fileName);
            if (!await CopyWithLimitAsync(response, path, cancellation))
            {
                File.Delete(path);
                return null;
            }
            return "images/" + fileName;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UriFormatException)
        {
            return null;   // 이 이미지는 원래 주소로 남긴다
        }
    }

    /// <summary>파일로 복사한다. 상한을 넘으면 false.</summary>
    private static async Task<bool> CopyWithLimitAsync(HttpResponseMessage response, string path, CancellationToken cancellation)
    {
        await using var source = await response.Content.ReadAsStreamAsync(cancellation);
        await using var target = File.Create(path);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            total += read;
            if (total > MaxImageBytes) return false;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
        return total > 0;
    }

    /// <summary>
    /// 응답 형식으로 확장자를 정한다. 형식을 알 수 없는 바이너리일 때만 주소의 확장자를 믿는다
    /// (오류 페이지 HTML 을 이미지로 저장하지 않도록).
    /// </summary>
    private static string? ImageExtension(string? mediaType, string url)
    {
        switch (mediaType?.ToLowerInvariant())
        {
            case "image/jpeg" or "image/jpg" or "image/pjpeg": return ".jpg";
            case "image/png": return ".png";
            case "image/gif": return ".gif";
            case "image/webp": return ".webp";
            case "image/avif": return ".avif";
            case "image/svg+xml": return ".svg";
            case "image/bmp": return ".bmp";
            case null or "application/octet-stream" or "binary/octet-stream":
                var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
                return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".avif" or ".svg" or ".bmp" ? extension : null;
            default:
                return null;
        }
    }

    private static void ReplaceDirectory(string source, string target)
    {
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        Directory.Move(source, target);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error(ex, $"보관 폴더 삭제: {path}");
        }
    }

    /// <summary>앱이 보관 도중 꺼졌다면 임시 폴더가 남는다. 시작할 때 지운다.</summary>
    private static void DeleteLeftoverTempFolders()
    {
        foreach (var directory in Directory.EnumerateDirectories(AppPaths.Archive, TempFolderPrefix + "*"))
            TryDeleteDirectory(directory);
    }
}
