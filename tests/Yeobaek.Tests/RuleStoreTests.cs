using Microsoft.Data.Sqlite;
using Yeobaek.Data;

namespace Yeobaek.Tests;

public sealed class RuleStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"yeobaek-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void Deleted_seed_rule_does_not_come_back_on_next_start()
    {
        var store = Start();
        var seed = store.GetRulesFor("example.com").First(rule => rule.Selector == ".adsbygoogle");
        store.DeleteRules([seed.Id]);

        var restarted = Start();

        Assert.DoesNotContain(restarted.GetRulesFor("example.com"), rule => rule.Selector == ".adsbygoogle");
    }

    [Fact]
    public void Upgrade_adds_only_newer_default_rules_and_keeps_deleted_older_ones_deleted()
    {
        var store = Start();
        var v1Rule = RuleStore.DefaultGlobalRules.First(batch => batch.Version == 1).Selectors[0];
        var v2Rule = RuleStore.DefaultGlobalRules.First(batch => batch.Version == 2).Selectors[0];
        store.DeleteRule(HostName.Global, v1Rule);
        store.DeleteRule(HostName.Global, v2Rule);
        SetSchemaVersion(1);   // 버전 1 앱이 만든 DB 인 것처럼 되돌린다

        var upgraded = Start().GetRulesFor("example.com").Select(rule => rule.Selector).ToList();

        Assert.Contains(v2Rule, upgraded);
        Assert.DoesNotContain(v1Rule, upgraded);
    }

    [Fact]
    public void Rule_for_site_applies_to_its_subdomains_but_not_to_other_sites()
    {
        var store = Start();
        store.AddRule("www.tistory.com", ".ad-slot");   // 저장 시 www 를 뗀다

        Assert.Contains(store.GetRulesFor("blog.tistory.com"), rule => rule.Selector == ".ad-slot");
        Assert.DoesNotContain(store.GetRulesFor("example.com"), rule => rule.Selector == ".ad-slot");
    }

    private RuleStore Start()
    {
        var database = new Database(_path);
        database.EnsureCreated(RuleStore.SeedGlobalRules);
        return new RuleStore(database);
    }

    private void SetSchemaVersion(int version)
    {
        using var connection = new Database(_path).Open();
        connection.Command($"PRAGMA user_version = {version};").ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
