using System.IO;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;

namespace Yeobaek.Reader;

/// <summary>
/// 3층 — 리더 모드. 추출한 본문(<see cref="ArticleExtractor"/>)을 파일로 저장하고
/// 가상 호스트(https://reader.yeobaek.example/...)로 서빙한다. NavigateToString 의 2MB 제한을 받지 않는다.
/// 리더 모드에서 바로 보관할 수 있도록 최근에 추출한 글 몇 개를 기억해 둔다.
/// </summary>
public sealed class ReaderService
{
    /// <summary>실제 사이트와 겹치지 않도록 RFC 6761 예약 도메인(.example)을 쓴다.</summary>
    public const string VirtualHost = "reader.yeobaek.example";

    private const int RecentArticleLimit = 20;
    private static readonly string ReaderUrlPrefix = $"https://{VirtualHost}/";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);

    private readonly SettingsFile _settings;
    private readonly Dictionary<string, string> _sourceByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ExtractedArticle> _recentArticles = new(StringComparer.Ordinal);
    private readonly Queue<string> _recentOrder = new();

    public ReaderService(SettingsFile settings)
    {
        _settings = settings;
        DeleteOldPages();
    }

    public static bool IsReaderUrl(string? url)
        => url is not null && url.StartsWith(ReaderUrlPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>탭마다 가상 호스트를 매핑한다. 다른 사이트는 이 파일들을 읽을 수 없다(Deny).</summary>
    public void Attach(CoreWebView2 core)
        => core.SetVirtualHostNameToFolderMapping(VirtualHost, AppPaths.ReaderCache, CoreWebView2HostResourceAccessKind.Deny);

    /// <summary>리더 페이지 주소면 원문 주소를, 아니면 null 을 돌려준다.</summary>
    public string? GetSourceUrl(string? url)
    {
        if (!IsReaderUrl(url)) return null;
        var path = url![ReaderUrlPrefix.Length..];
        var queryStart = path.IndexOfAny(['?', '#']);
        if (queryStart >= 0) path = path[..queryStart];
        return _sourceByPath.GetValueOrDefault(path);
    }

    /// <summary>
    /// 지금 문서에서 본문을 추출해 리더 페이지를 만들고 그 주소를 돌려준다.
    /// 본문을 찾지 못하면 null (SPA·이미지 위주 페이지 등).
    /// </summary>
    public async Task<string?> CreateReaderPageAsync(CoreWebView2 core)
    {
        var article = await ArticleExtractor.ExtractAsync(core);
        if (article is null) return null;
        Remember(article);

        var sourceUrl = article.Content.SourceUrl;
        var fileName = ArticleExtractor.KeyFor(sourceUrl) + ".html";
        var page = ReaderPage.Build(article.Content, _settings.Current.Reader);
        await File.WriteAllTextAsync(Path.Combine(AppPaths.ReaderCache, fileName), page, Encoding.UTF8);
        _sourceByPath[fileName] = sourceUrl;

        // 같은 글을 다시 추출했을 때 캐시된 옛 파일이 보이지 않도록 쿼리를 바꾼다
        return $"{ReaderUrlPrefix}{fileName}?v={DateTime.UtcNow.Ticks}";
    }

    /// <summary>이 원문으로 최근에 만든 리더 페이지의 추출 결과. 없으면 null.</summary>
    public ExtractedArticle? GetRecentArticle(string sourceUrl) => _recentArticles.GetValueOrDefault(sourceUrl);

    private void Remember(ExtractedArticle article)
    {
        var sourceUrl = article.Content.SourceUrl;
        if (!_recentArticles.ContainsKey(sourceUrl)) _recentOrder.Enqueue(sourceUrl);
        _recentArticles[sourceUrl] = article;
        while (_recentOrder.Count > RecentArticleLimit) _recentArticles.Remove(_recentOrder.Dequeue());
    }

    private static void DeleteOldPages()
    {
        try
        {
            foreach (var file in new DirectoryInfo(AppPaths.ReaderCache).EnumerateFiles("*.html"))
            {
                if (DateTime.UtcNow - file.LastWriteTimeUtc > CacheLifetime) file.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error(ex, "리더 캐시 정리");
        }
    }
}
