using System.Globalization;
using System.Net;
using Yeobaek.Data;

namespace Yeobaek.Reader;

/// <summary>리더 페이지에 넣을 본문. ContentHtml 은 추출기가 정리한 HTML 이고 나머지는 일반 텍스트다.</summary>
public sealed record ReaderContent(
    string Title,
    string? Byline,
    string? SiteName,
    int MinutesToRead,
    string ContentHtml,
    string SourceUrl,
    string? Language);

/// <summary>추출한 본문으로 읽기 전용 HTML 문서를 만든다.</summary>
public static class ReaderPage
{
    // 스크립트는 전혀 실행하지 않는다(본문에 섞여 온 인라인 핸들러 포함). 이미지·영상·글꼴은 어디서든 받는다.
    private const string ContentSecurityPolicy =
        "default-src 'none'; img-src * data: blob:; media-src *; font-src * data:; style-src 'unsafe-inline'; " +
        "frame-src https:; form-action 'none'; base-uri 'none'";

    /// <param name="archivedAt">보관본이면 보관한 시각. 제목 아래에 보관본임을 적는다.</param>
    public static string Build(ReaderContent content, ReaderSettings settings, DateTime? archivedAt = null)
    {
        var archivedNote = archivedAt is { } time ? $"{time:yyyy-MM-dd} 보관본" : null;
        var meta = string.Join(" · ", new[] { archivedNote, content.SiteName, content.Byline, $"약 {content.MinutesToRead}분" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => Encode(part!)));

        // 이미지 서버가 다른 사이트의 참조(Referer)를 막는 경우가 많아 보내지 않는다
        return $$"""
            <!doctype html>
            <html lang="{{Encode(content.Language ?? "ko")}}">
            <head>
            <meta charset="utf-8">
            <meta http-equiv="Content-Security-Policy" content="{{ContentSecurityPolicy}}">
            <meta name="referrer" content="no-referrer">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{Encode(content.Title)}}</title>
            <style>{{Css(settings)}}</style>
            </head>
            <body>
            <header>
              <h1>{{Encode(content.Title)}}</h1>
              <div class="meta">{{meta}}</div>
              <a class="source" href="{{Encode(content.SourceUrl)}}">원문 보기</a>
            </header>
            <article>
            {{content.ContentHtml}}
            </article>
            </body>
            </html>
            """;
    }

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private static string Css(ReaderSettings settings)
    {
        var fontSize = Math.Clamp(settings.FontSizePx, 12, 32);
        var lineHeight = Math.Clamp(settings.LineHeight, 1.2, 2.6);
        var maxWidth = Math.Clamp(settings.MaxWidthRem, 28, 80);
        var fontFamily = SanitizeFontFamily(settings.FontFamily);
        var colorScheme = settings.Theme?.ToLowerInvariant() switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "light dark",
        };

        return string.Create(CultureInfo.InvariantCulture, $$"""
            :root { color-scheme: {{colorScheme}}; --muted: color-mix(in srgb, CanvasText 58%, Canvas); --line: color-mix(in srgb, CanvasText 18%, Canvas); }
            body { max-width: {{maxWidth}}rem; margin: 3rem auto 6rem; padding: 0 1.25rem;
                   font: {{fontSize}}px/{{lineHeight}} {{fontFamily}};
                   word-break: keep-all; overflow-wrap: anywhere; background: Canvas; color: CanvasText; }
            h1 { font-size: 1.75em; line-height: 1.35; margin: 0 0 .5rem; }
            h2, h3 { line-height: 1.4; margin-top: 2.2em; }
            .meta { color: var(--muted); font-size: .85em; }
            .source { display: inline-block; margin: .35rem 0 2.5rem; color: var(--muted); font-size: .85em; }
            a { color: LinkText; }
            img, video { max-width: 100%; height: auto; border-radius: .5rem; }
            iframe { width: 100%; aspect-ratio: 16 / 9; height: auto; border: 0; border-radius: .5rem; }
            figure { margin: 1.75rem 0; }
            figcaption { color: var(--muted); font-size: .85em; margin-top: .4rem; }
            pre { background: #1e1e1e; color: #e6e6e6; padding: 1rem; border-radius: .5rem;
                  overflow-x: auto; line-height: 1.6; font-size: .88em; word-break: normal; overflow-wrap: normal; }
            code { font-family: "D2Coding", Consolas, monospace; }
            blockquote { border-left: 3px solid var(--line); margin-left: 0; padding-left: 1rem; color: var(--muted); }
            table { border-collapse: collapse; display: block; max-width: 100%; overflow-x: auto; }
            th, td { border: 1px solid var(--line); padding: .4rem .65rem; }
            hr { border: 0; border-top: 1px solid var(--line); margin: 2.5rem 0; }
            """);
    }

    /// <summary>설정 파일에서 온 글꼴 목록이 CSS 규칙 밖으로 새지 않게 한다.</summary>
    private static string SanitizeFontFamily(string? fontFamily)
    {
        var cleaned = new string((fontFamily ?? "").Where(ch => ch is not (';' or '{' or '}' or '<' or '>') && !char.IsControl(ch)).ToArray()).Trim();
        return cleaned.Length > 0 ? cleaned : "system-ui, sans-serif";
    }
}
