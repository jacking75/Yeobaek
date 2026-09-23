using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;

namespace Yeobaek.Reader;

/// <summary>추출한 글. Content 는 리더 페이지에 그릴 내용, PlainText 는 검색 색인에 넣을 본문 글자.</summary>
public sealed record ExtractedArticle(ReaderContent Content, string PlainText);

/// <summary>
/// 탭에 이미 그려진 문서의 DOM 에서 본문을 추출한다(SmartReader).
/// 다시 내려받지 않으므로 로그인이 필요한 글이나 스크립트로 그려지는 글에서도 동작한다.
/// </summary>
public static class ArticleExtractor
{
    /// <summary>본문을 찾지 못하면 null (SPA·이미지 위주 페이지 등).</summary>
    public static async Task<ExtractedArticle?> ExtractAsync(CoreWebView2 core)
    {
        var sourceUrl = core.Source;
        if (!SafeUrl.IsWebUrl(sourceUrl)) return null;

        var json = await core.ExecuteScriptAsync("document.documentElement ? document.documentElement.outerHTML : ''");
        var html = JsonSerializer.Deserialize<string>(json);
        if (string.IsNullOrEmpty(html)) return null;

        // 파싱은 큰 문서에서 수백 ms 가 걸릴 수 있어 UI 스레드 밖에서 한다
        return await Task.Run(() => Parse(sourceUrl, html));
    }

    /// <summary>원문 주소마다 정해지는 짧은 이름. 리더 페이지 파일·보관 폴더 이름에 쓴다.</summary>
    public static string KeyFor(string sourceUrl)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sourceUrl)))[..20];

    private static ExtractedArticle? Parse(string sourceUrl, string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        LazyImages.Promote(document);
        var article = new SmartReader.Reader(sourceUrl, document).GetArticle();
        if (!article.IsReadable || string.IsNullOrWhiteSpace(article.Content)) return null;

        var content = new ReaderContent(
            Title: string.IsNullOrWhiteSpace(article.Title) ? sourceUrl : article.Title,
            Byline: string.IsNullOrWhiteSpace(article.Author) ? article.Byline : article.Author,
            SiteName: article.SiteName,
            MinutesToRead: Math.Max(1, (int)Math.Round(article.TimeToRead.TotalMinutes)),
            ContentHtml: article.Content,
            SourceUrl: sourceUrl,
            Language: string.IsNullOrWhiteSpace(article.Language) ? null : article.Language);
        return new ExtractedArticle(content, article.TextContent ?? "");
    }
}
