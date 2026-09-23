namespace Yeobaek.Data;

/// <summary>
/// 숨김 규칙·예외 사이트에서 쓰는 호스트 이름 규칙.
/// 저장할 때는 소문자로 바꾸고 앞의 "www." 를 떼어, www 유무와 상관없이 같은 사이트로 본다.
/// </summary>
public static class HostName
{
    /// <summary>모든 사이트에 적용되는 전역 규칙의 호스트 값.</summary>
    public const string Global = "*";

    public static string Normalize(string host)
    {
        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        var hasWwwPrefix = normalized.StartsWith("www.", StringComparison.Ordinal)
                           && normalized.IndexOf('.', 4) > 0;   // "www.com" 같은 경우는 그대로 둔다
        return hasWwwPrefix ? normalized[4..] : normalized;
    }

    /// <summary>http/https URL 에서 정규화된 호스트를 꺼낸다. 웹 주소가 아니면 null.</summary>
    public static string? FromUrl(string? url)
    {
        if (!SafeUrl.TryParseWebUrl(url, out var uri)) return null;
        var host = Normalize(uri.IdnHost);   // 페이지의 location.hostname 과 같은 퓨니코드 형태
        return host.Length > 0 ? host : null;
    }

    /// <summary>host 가 domain 과 같거나 그 하위 도메인이면 true. domain 이 "*" 면 항상 true.</summary>
    public static bool Matches(string host, string domain)
    {
        if (domain == Global) return true;
        if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)) return true;
        return host.Length > domain.Length
               && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase)
               && host[host.Length - domain.Length - 1] == '.';
    }
}
