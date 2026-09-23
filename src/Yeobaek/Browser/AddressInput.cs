using Yeobaek.Data;

namespace Yeobaek.Browser;

/// <summary>주소창 입력을 이동할 URL 로 바꾼다. 주소처럼 보이지 않으면 검색 URL 을 만든다.</summary>
public static class AddressInput
{
    /// <summary>빈 입력이면 null. javascript:, file: 같은 주소는 검색어로 취급한다.</summary>
    public static string? Resolve(string text, string searchUrlTemplate)
    {
        var input = text.Trim();
        if (input.Length == 0) return null;
        if (input.Equals("about:blank", StringComparison.OrdinalIgnoreCase)) return "about:blank";

        if (SafeUrl.TryParseWebUrl(input, out var absolute)) return absolute.AbsoluteUri;
        if (!input.Contains(' ') && LooksLikeHost(input, out var withScheme)) return withScheme;

        var template = searchUrlTemplate.Contains("{0}") ? searchUrlTemplate : AppSettings.DefaultSearchUrl;
        return template.Replace("{0}", Uri.EscapeDataString(input));
    }

    /// <summary>
    /// "example.com/path", "localhost:8080", "192.168.0.1" 처럼 스킴 없이 적은 주소인지 본다.
    /// "3.14" 같은 숫자는 주소로 보지 않도록 마지막 이름이 글자로 된 도메인이거나 완전한 IPv4 여야 한다.
    /// </summary>
    private static bool LooksLikeHost(string input, out string url)
    {
        url = "";
        var hostEnd = input.IndexOfAny(['/', '?', '#', ':']);
        var typedHost = hostEnd < 0 ? input : input[..hostEnd];
        var isLocalhost = typedHost.Equals("localhost", StringComparison.OrdinalIgnoreCase);
        if (!isLocalhost && !IsFullIPv4(typedHost) && !HasAlphabeticTld(typedHost)) return false;

        var candidate = (isLocalhost ? "http://" : "https://") + input;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return false;

        url = uri.AbsoluteUri;
        return true;
    }

    private static bool IsFullIPv4(string host)
    {
        var parts = host.Split('.');
        return parts.Length == 4 && parts.All(part => byte.TryParse(part, out _));
    }

    private static bool HasAlphabeticTld(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 2 && parts.All(part => part.Length > 0) && parts[^1].Length >= 2 && parts[^1].All(char.IsLetter);
    }
}
