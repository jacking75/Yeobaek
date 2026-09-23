using Yeobaek.Browser;

namespace Yeobaek.Tests;

public class BlockRulesTests
{
    private static readonly BlockRules Rules = BlockRules.Parse(
    [
        "# 주석",
        "",
        "  ad.doubleclick.net  ",
        "/adfit/",
    ]);

    [Theory]
    [InlineData("https://ad.doubleclick.net/pixel", true)]
    [InlineData("https://x.y.ad.doubleclick.net/pixel", true)]             // 하위 도메인
    [InlineData("https://AD.DoubleClick.NET/", true)]                      // 대소문자
    [InlineData("https://user:pw@ad.doubleclick.net:8443/a", true)]        // 권한 정보·포트
    [InlineData("https://notad.doubleclick.net/", false)]                  // 이름만 비슷한 호스트
    [InlineData("https://ad.doubleclick.net.evil.com/", false)]            // 접미사가 아닌 경우
    [InlineData("https://doubleclick.net/", false)]                        // 상위 도메인은 막지 않음
    [InlineData("https://blog.example.com/js/AdFit/sdk.js", true)]         // 경로 힌트
    [InlineData("https://blog.example.com/page?next=/adfit/", false)]      // 쿼리는 보지 않음
    [InlineData("https://adfit.example.com/", false)]                      // 호스트는 경로 힌트 대상 아님
    [InlineData("data:text/html,/adfit/", false)]
    [InlineData("about:blank", false)]
    public void ShouldBlock_matches_hosts_with_subdomains_and_path_hints_only(string url, bool expected)
        => Assert.Equal(expected, Rules.ShouldBlock(url));

    [Fact]
    public void User_lines_add_to_built_in_list_and_bang_removes_a_built_in_entry()
    {
        var rules = BlockRules.WithBuiltIn(["ads.example.com", "!ads-partners.coupang.com", "!/adfit/"]);

        Assert.True(rules.ShouldBlock("https://ads.example.com/x.js"));                   // 사용자가 더한 항목
        Assert.True(rules.ShouldBlock("https://pagead2.googlesyndication.com/x.js"));     // 내장 항목은 그대로
        Assert.False(rules.ShouldBlock("https://ads-partners.coupang.com/g.js"));         // 사용자가 뺀 내장 호스트
        Assert.False(rules.ShouldBlock("https://blog.example.com/adfit/sdk.js"));         // 사용자가 뺀 내장 경로 힌트
    }
}
