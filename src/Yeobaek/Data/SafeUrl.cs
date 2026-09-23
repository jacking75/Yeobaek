using System.Diagnostics.CodeAnalysis;

namespace Yeobaek.Data;

/// <summary>
/// 페이지·사용자에게서 받은 URL 검증. 탭으로 열거나 저장하는 주소는 http/https 만 허용한다
/// (file:, javascript: 등은 거부).
/// </summary>
public static class SafeUrl
{
    public static bool IsWebUrl([NotNullWhen(true)] string? url) => TryParseWebUrl(url, out _);

    public static bool TryParseWebUrl([NotNullWhen(true)] string? url, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        uri = parsed;
        return true;
    }
}
