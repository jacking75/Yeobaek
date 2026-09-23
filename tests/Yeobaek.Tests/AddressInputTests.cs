using Yeobaek.Browser;

namespace Yeobaek.Tests;

public class AddressInputTests
{
    private const string Search = "https://search.test/?q={0}";

    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("example.com/path?a=1", "https://example.com/path?a=1")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("localhost:8080", "http://localhost:8080/")]
    [InlineData("192.168.0.1", "https://192.168.0.1/")]
    [InlineData("3.14", "https://search.test/?q=3.14")]                              // 숫자는 검색
    [InlineData("hello world", "https://search.test/?q=hello%20world")]
    [InlineData("javascript:alert(1)", "https://search.test/?q=javascript%3Aalert%281%29")]  // 스크립트 주소로 이동하지 않음
    [InlineData("file:///C:/Windows/win.ini", "https://search.test/?q=file%3A%2F%2F%2FC%3A%2FWindows%2Fwin.ini")]
    public void Resolve_opens_web_addresses_and_searches_everything_else(string input, string expected)
        => Assert.Equal(expected, AddressInput.Resolve(input, Search));
}
