using AngleSharp.Dom;

namespace Yeobaek.Reader;

/// <summary>
/// 지연 로딩 이미지의 진짜 주소를 src 로 올린다.
/// 티스토리 등은 화면 밖 이미지의 src 에 1픽셀 자리표시(blank.png)를 두고 진짜 주소를 data-src 에 둔다.
/// SmartReader 는 src 가 이미 있으면 바꾸지 않아, 리더 모드·보관본에 빈 이미지가 들어간다.
/// </summary>
public static class LazyImages
{
    private static readonly string[] SourceAttributes =
        ["data-src", "data-lazy-src", "data-original", "data-lazy", "data-url", "data-actualsrc"];

    private static readonly string[] SrcsetAttributes = ["data-srcset", "data-lazy-srcset"];

    public static void Promote(IDocument document)
    {
        foreach (var image in document.QuerySelectorAll("img"))
        {
            if (FirstUsable(image, SourceAttributes) is { } source) image.SetAttribute("src", source);
            if (FirstUsable(image, SrcsetAttributes) is { } srcset) image.SetAttribute("srcset", srcset);
        }
    }

    private static string? FirstUsable(IElement image, string[] attributes)
    {
        foreach (var name in attributes)
        {
            var value = image.GetAttribute(name)?.Trim();
            if (!string.IsNullOrEmpty(value) && !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return value;
        }
        return null;
    }
}
