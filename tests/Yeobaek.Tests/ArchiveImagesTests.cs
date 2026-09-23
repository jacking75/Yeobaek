using System.Collections.Concurrent;
using Yeobaek.Archive;

namespace Yeobaek.Tests;

public class ArchiveImagesTests
{
    [Fact]
    public async Task Localize_points_images_to_saved_files_and_keeps_failed_ones_online()
    {
        const string html = """
            <p><img src="https://img.test/a.jpg" srcset="https://img.test/a-2x.jpg 2x"></p>
            <p><img src="https://img.test/a.jpg"></p>
            <picture><source srcset="https://img.test/b.webp"><img src="https://img.test/b.jpg"></picture>
            <p><img src="https://img.test/broken.png"></p>
            <p><img src="data:image/gif;base64,R0lGODlhAQABAAAAACw="></p>
            """;
        var requested = new ConcurrentBag<string>();

        var result = await ArchiveImages.LocalizeAsync(html, (url, index) =>
        {
            requested.Add(url);
            return Task.FromResult(url.Contains("broken") ? null : $"images/{index:000}.jpg");
        });

        Assert.Equal(3, requested.Count);                                   // 같은 주소는 한 번만, data: 는 받지 않음
        Assert.Equal((3, 2), (result.Total, result.Saved));
        Assert.DoesNotContain("srcset", result.Html);                        // 원격 이미지를 고르지 않도록
        Assert.DoesNotContain("https://img.test/a.jpg", result.Html);        // 두 곳 모두 로컬로
        Assert.DoesNotContain("https://img.test/b.jpg", result.Html);
        Assert.Contains("src=\"https://img.test/broken.png\"", result.Html); // 실패한 이미지는 원래 주소 그대로
        Assert.Contains("data:image/gif", result.Html);
    }
}
