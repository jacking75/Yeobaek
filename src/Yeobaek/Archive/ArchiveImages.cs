using AngleSharp.Html.Parser;

namespace Yeobaek.Archive;

/// <summary>본문 HTML 속 이미지를 내려받아 로컬 파일을 가리키도록 바꾼다(오프라인 보관용).</summary>
public static class ArchiveImages
{
    public const int MaxImages = 300;
    private const int MaxConcurrentDownloads = 4;

    /// <param name="Html">바뀐 본문 HTML</param>
    /// <param name="Total">내려받으려 한 이미지 수(같은 주소는 한 번)</param>
    /// <param name="Saved">로컬로 바꾼 이미지 수. 나머지는 원래 주소로 남아 인터넷이 있을 때만 보인다.</param>
    public sealed record Result(string Html, int Total, int Saved);

    /// <param name="download">
    /// (이미지 주소, 1부터 시작하는 순번) → 저장한 파일의 상대 경로(예: "images/001.jpg"). 실패하면 null.
    /// </param>
    public static async Task<Result> LocalizeAsync(string html, Func<string, int, Task<string?>> download)
    {
        var document = new HtmlParser().ParseDocument($"<!doctype html><html><body>{html}</body></html>");
        var body = document.Body!;

        // srcset 과 <picture> 의 <source> 가 있으면 브라우저가 src 대신 원격 이미지를 고른다. 지운다.
        foreach (var source in body.QuerySelectorAll("picture > source").ToList()) source.Remove();
        var images = body.QuerySelectorAll("img[src]").ToList();
        foreach (var image in images)
        {
            image.RemoveAttribute("srcset");
            image.RemoveAttribute("sizes");
        }

        var urls = images
            .Select(image => image.GetAttribute("src")!.Trim())
            .Where(IsWebUrl)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxImages)
            .ToList();

        var localPaths = new string?[urls.Count];
        using var gate = new SemaphoreSlim(MaxConcurrentDownloads);
        await Task.WhenAll(urls.Select(async (url, index) =>
        {
            await gate.WaitAsync();
            try
            {
                localPaths[index] = await download(url, index + 1);
            }
            finally
            {
                gate.Release();
            }
        }));

        var localByUrl = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < urls.Count; i++)
        {
            if (localPaths[i] is { } local) localByUrl[urls[i]] = local;
        }
        foreach (var image in images)
        {
            if (localByUrl.TryGetValue(image.GetAttribute("src")!.Trim(), out var local)) image.SetAttribute("src", local);
        }

        return new Result(body.InnerHtml, urls.Count, localByUrl.Count);
    }

    private static bool IsWebUrl(string url)
        => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
}
