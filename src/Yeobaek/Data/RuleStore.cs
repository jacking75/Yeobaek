using Microsoft.Data.Sqlite;

using Yeobaek.Ui;

namespace Yeobaek.Data;

public sealed record RuleEntry(long Id, string Host, string Selector, DateTime AddedAt)
{
    public string ScopeLabel => Host == HostName.Global ? StringTable.Get("Rules.AllSites") : Host;
}

/// <summary>요소 숨김 규칙과 차단 예외 사이트(allowlist) 저장소.</summary>
public sealed class RuleStore
{
    /// <summary>
    /// 기본 전역 숨김 규칙. 버전별 묶음으로 두어, 앱을 업데이트하면 새로 더해진 묶음만 심는다.
    /// 새 묶음을 더하면 <see cref="Database.SchemaVersion"/> 도 올린다.
    /// </summary>
    public static readonly (int Version, string[] Selectors)[] DefaultGlobalRules =
    [
        (1,
        [
            "ins.adsbygoogle",
            ".adsbygoogle",
            ".google-auto-placed",
            "iframe[id^=\"aswift_\"]",
            "iframe[id^=\"google_ads_iframe\"]",
            ".revenue_unit_wrap",
            ".revenue_unit_item",
            ".kakao_ad_area",
        ]),
        (2,
        [
            // 쿠팡 파트너스 위젯. 스크립트는 1층에서 막지만 예외 사이트가 아닌 곳에 남은 틀까지 숨긴다.
            "iframe[src*=\"ads-partners.coupang.com\"]",
        ]),
    ];

    private readonly Database _database;

    // 예외 사이트는 탐색마다 조회하므로 메모리에 들고 있는다. 바뀔 때마다 새 목록으로 교체한다.
    private IReadOnlyList<string> _allowlist;

    public event Action? RulesChanged;
    public event Action? AllowlistChanged;

    public RuleStore(Database database)
    {
        _database = database;
        _allowlist = LoadAllowlist();
    }

    /// <summary>fromVersion 이후에 더해진 기본 규칙 묶음만 심는다(처음 만들 때는 0 이라 전부).</summary>
    public static void SeedGlobalRules(SqliteConnection connection, SqliteTransaction transaction, int fromVersion)
    {
        foreach (var (version, selectors) in DefaultGlobalRules)
        {
            if (version <= fromVersion) continue;
            foreach (var selector in selectors)
            {
                connection.Command(transaction,
                    "INSERT OR IGNORE INTO rules(host, selector, added_at) VALUES($host, $selector, $now)",
                    ("$host", HostName.Global), ("$selector", selector), ("$now", Database.Now())).ExecuteNonQuery();
            }
        }
    }

    // ── 숨김 규칙 ─────────────────────────────────────────────

    /// <summary>{ "*": [...], "tistory.com": [...] } 형태. 주입 스크립트가 location.hostname 으로 고른다.</summary>
    public Dictionary<string, List<string>> GetAllGroupedByHost()
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var rule in ReadAllRules())
        {
            if (!map.TryGetValue(rule.Host, out var selectors)) map[rule.Host] = selectors = [];
            selectors.Add(rule.Selector);
        }
        return map;
    }

    /// <summary>host 에 적용되는 규칙 (전역, 상위 도메인 규칙 포함).</summary>
    public List<RuleEntry> GetRulesFor(string host)
    {
        var normalized = HostName.Normalize(host);
        return ReadAllRules().Where(rule => HostName.Matches(normalized, rule.Host)).ToList();
    }

    /// <summary>규칙을 추가한다. 이미 있거나 선택자가 올바르지 않으면 false.</summary>
    public bool AddRule(string host, string selector)
    {
        var scope = host == HostName.Global ? host : HostName.Normalize(host);
        selector = selector.Trim();
        if (scope.Length == 0 || !SelectorRules.IsAcceptable(selector)) return false;

        using var connection = _database.Open();
        var added = connection.Command(
            "INSERT OR IGNORE INTO rules(host, selector, added_at) VALUES($host, $selector, $now)",
            ("$host", scope), ("$selector", selector), ("$now", Database.Now())).ExecuteNonQuery() > 0;

        if (added) RulesChanged?.Invoke();
        return added;
    }

    public void DeleteRule(string host, string selector)
    {
        using var connection = _database.Open();
        connection.Command("DELETE FROM rules WHERE host = $host AND selector = $selector",
            ("$host", host), ("$selector", selector)).ExecuteNonQuery();
        RulesChanged?.Invoke();
    }

    public void DeleteRules(IEnumerable<long> ids)
    {
        using var connection = _database.Open();
        foreach (var id in ids)
            connection.Command("DELETE FROM rules WHERE id = $id", ("$id", id)).ExecuteNonQuery();
        RulesChanged?.Invoke();
    }

    private List<RuleEntry> ReadAllRules()
    {
        using var connection = _database.Open();
        using var reader = connection.Command(
            "SELECT id, host, selector, added_at FROM rules ORDER BY host, id").ExecuteReader();

        var rules = new List<RuleEntry>();
        while (reader.Read())
        {
            rules.Add(new RuleEntry(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                Database.ParseTime(reader.GetString(3))));
        }
        return rules;
    }

    // ── 차단 예외 사이트 ──────────────────────────────────────

    public IReadOnlyList<string> GetAllowlist() => _allowlist;

    public bool IsAllowlisted(string host) => FindAllowlistEntry(host) is not null;

    /// <summary>host 를 예외로 만든 항목(자기 자신 또는 상위 도메인). 가장 구체적인 항목을 돌려준다.</summary>
    public string? FindAllowlistEntry(string host)
    {
        var normalized = HostName.Normalize(host);
        return _allowlist
            .Where(domain => HostName.Matches(normalized, domain))
            .OrderByDescending(domain => domain.Length)
            .FirstOrDefault();
    }

    public void SetAllowlisted(string host, bool allowed)
    {
        var normalized = HostName.Normalize(host);
        if (normalized.Length == 0 || normalized == HostName.Global) return;

        using (var connection = _database.Open())
        {
            if (allowed)
                connection.Command("INSERT OR IGNORE INTO allowlist(host, added_at) VALUES($host, $now)",
                    ("$host", normalized), ("$now", Database.Now())).ExecuteNonQuery();
            else
                connection.Command("DELETE FROM allowlist WHERE host = $host", ("$host", normalized)).ExecuteNonQuery();
        }

        _allowlist = LoadAllowlist();
        AllowlistChanged?.Invoke();
    }

    private List<string> LoadAllowlist()
    {
        using var connection = _database.Open();
        using var reader = connection.Command("SELECT host FROM allowlist ORDER BY host").ExecuteReader();
        var hosts = new List<string>();
        while (reader.Read()) hosts.Add(reader.GetString(0));
        return hosts;
    }
}
