using AngleSharp.Html.Parser;
using Yeobaek.Reader;

namespace Yeobaek.Tests;

public class LazyImagesTests
{
    [Fact]
    public void Promote_replaces_placeholder_src_with_lazy_source()
    {
        var document = new HtmlParser().ParseDocument("""
            <img id="lazy" src="https://blog.test/blank.png" data-src="https://cdn.test/real.png" data-srcset="https://cdn.test/real-2x.png 2x">
            <img id="normal" src="https://cdn.test/plain.png">
            <img id="inline" src="https://cdn.test/keep.png" data-src="data:image/gif;base64,R0lGOD">
            """);

        LazyImages.Promote(document);

        Assert.Equal("https://cdn.test/real.png", document.GetElementById("lazy")!.GetAttribute("src"));
        Assert.Equal("https://cdn.test/real-2x.png 2x", document.GetElementById("lazy")!.GetAttribute("srcset"));
        Assert.Equal("https://cdn.test/plain.png", document.GetElementById("normal")!.GetAttribute("src"));
        Assert.Equal("https://cdn.test/keep.png", document.GetElementById("inline")!.GetAttribute("src"));   // data: 자리표시는 무시
    }
}
