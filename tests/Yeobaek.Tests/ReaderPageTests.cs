using Yeobaek.Data;
using Yeobaek.Reader;

namespace Yeobaek.Tests;

public class ReaderPageTests
{
    [Fact]
    public void Build_escapes_page_supplied_text_and_blocks_scripts()
    {
        var content = new ReaderContent(
            Title: "<script>alert(1)</script>제목",
            Byline: "\"><img src=x onerror=alert(1)>",
            SiteName: null,
            MinutesToRead: 3,
            ContentHtml: "<p>본문</p>",
            SourceUrl: "https://example.com/post?a=1&b=\"2\"",
            Language: null);

        var html = ReaderPage.Build(content, new ReaderSettings());

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;제목", html);
        Assert.Contains("default-src 'none'", html);   // 본문에 섞여 온 스크립트도 실행되지 않는다
    }
}
