using Yeobaek.Data;

namespace Yeobaek.Tests;

public class HostNameTests
{
    [Theory]
    [InlineData("https://WWW.Example.com/path", "example.com")]
    [InlineData("https://blog.example.com", "blog.example.com")]
    [InlineData("https://www.com/", "www.com")]       // 도메인 자체가 www 면 떼지 않음
    [InlineData("file:///C:/a.html", null)]
    [InlineData("not a url", null)]
    public void FromUrl_normalizes_web_hosts(string url, string? expected)
        => Assert.Equal(expected, HostName.FromUrl(url));

    [Theory]
    [InlineData("example.com", "example.com", true)]
    [InlineData("blog.example.com", "example.com", true)]
    [InlineData("badexample.com", "example.com", false)]
    [InlineData("example.com", "blog.example.com", false)]
    [InlineData("anything.test", "*", true)]
    public void Matches_same_host_or_subdomain(string host, string domain, bool expected)
        => Assert.Equal(expected, HostName.Matches(host, domain));
}
