namespace Yeobaek.Browser;

/// <summary>
/// 1층 차단 판정. WebResourceRequested 핸들러 안에서 요청마다 불리므로
/// 정규식·LINQ·Uri 파싱 없이 URL 문자열 조각(span)만 보고 마이크로초 안에 끝낸다.
/// </summary>
public sealed class BlockRules
{
    /// <summary>
    /// 여백에 내장된 기본 목록. 앱을 업데이트하면 함께 바뀐다.
    /// blocklist.txt 에는 사용자가 더하거나('!' 로) 빼는 항목만 둔다.
    ///
    /// fundingchoicesmessages.google.com(광고 차단 복구)은 넣지 않는다. 막으면 페이지에 심긴
    /// 오류 보호 코드가 "광고 차단 소프트웨어가 방해하고 있습니다" 경고를 띄운다(티스토리에서 확인).
    /// </summary>
    public static readonly string[] BuiltIn =
    [
        // Google 광고
        "pagead2.googlesyndication.com",
        "tpc.googlesyndication.com",
        "googleads.g.doubleclick.net",
        "securepubads.g.doubleclick.net",
        "ad.doubleclick.net",
        "adservice.google.com",
        "www.googletagservices.com",
        "/pagead/js/adsbygoogle.js",
        // 카카오·다음
        "display.ad.daum.net",
        "analytics.ad.daum.net",
        "/adfit/",
        "/kas/static/ba.min.js",
        // 네이버
        "/melona/libs/",
        // 쿠팡 파트너스 위젯
        "ads-partners.coupang.com",
        "logs-partners.coupang.com",
        // 기타
        "static.criteo.net",
        "cas.criteo.com",
    ];

    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _hosts;
    private readonly string[] _pathHints;

    private BlockRules(HashSet<string> hosts, IEnumerable<string> pathHints)
    {
        _hosts = hosts.GetAlternateLookup<ReadOnlySpan<char>>();
        _pathHints = pathHints.ToArray();
    }

    public int Count => _hosts.Set.Count + _pathHints.Length;

    /// <summary>내장 기본 목록 뒤에 사용자 줄(blocklist.txt)을 적용해 만든다.</summary>
    public static BlockRules WithBuiltIn(IEnumerable<string> userLines) => Parse(BuiltIn.Concat(userLines));

    /// <summary>
    /// 한 줄에 하나. '#' 으로 시작하면 주석, '/' 로 시작하면 경로 힌트(경로에 포함되면 차단),
    /// 나머지는 호스트(하위 도메인까지 차단). 앞에 '!' 를 붙이면 앞서 나온 같은 항목을 뺀다.
    /// </summary>
    public static BlockRules Parse(IEnumerable<string> lines)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hints = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var remove = line.StartsWith('!');
            if (remove) line = line[1..].Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith('/'))
            {
                if (remove) hints.RemoveAll(hint => hint.Equals(line, StringComparison.OrdinalIgnoreCase));
                else hints.Add(line);
            }
            else
            {
                var host = line.TrimEnd('.');
                if (remove) hosts.Remove(host);
                else hosts.Add(host);
            }
        }
        return new BlockRules(hosts, hints);
    }

    public bool ShouldBlock(string url)
    {
        if (!TryGetHostAndPath(url, out var host, out var path)) return false;
        return IsBlockedHost(host) || ContainsPathHint(path);
    }

    private bool IsBlockedHost(ReadOnlySpan<char> host)
    {
        // "x.ads.example.com" → "ads.example.com" → "example.com" → "com" 순으로 올라가며 본다
        while (true)
        {
            if (_hosts.Contains(host)) return true;
            var dot = host.IndexOf('.');
            if (dot < 0) return false;
            host = host[(dot + 1)..];
        }
    }

    private bool ContainsPathHint(ReadOnlySpan<char> path)
    {
        foreach (var hint in _pathHints)
        {
            if (path.Contains(hint, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// "scheme://user@host:port/path?query#frag" 에서 host 와 path 만 잘라낸다.
    /// 권한 정보(user@)와 포트는 버리고, IPv6 주소([..])는 괄호째 host 로 본다.
    /// </summary>
    private static bool TryGetHostAndPath(string url, out ReadOnlySpan<char> host, out ReadOnlySpan<char> path)
    {
        host = default;
        path = default;

        var span = url.AsSpan();
        var schemeEnd = span.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0) return false;

        var rest = span[(schemeEnd + 3)..];
        var authorityEnd = rest.IndexOfAny('/', '?', '#');
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        var afterAuthority = authorityEnd < 0 ? ReadOnlySpan<char>.Empty : rest[authorityEnd..];

        var at = authority.LastIndexOf('@');
        if (at >= 0) authority = authority[(at + 1)..];

        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']');
            host = close < 0 ? authority : authority[..(close + 1)];
        }
        else
        {
            var colon = authority.IndexOf(':');
            host = colon < 0 ? authority : authority[..colon];
        }
        host = host.TrimEnd('.');

        var pathEnd = afterAuthority.IndexOfAny('?', '#');
        path = pathEnd < 0 ? afterAuthority : afterAuthority[..pathEnd];
        return !host.IsEmpty;
    }
}
