using Yeobaek.Browser;

namespace Yeobaek.Tests;

public class HostMessageTests
{
    private const string Token = "SESSION";

    [Fact]
    public void Parse_accepts_message_from_injected_script()
    {
        var message = HostMessage.Parse("""{"type":"open-background","token":"SESSION","url":"https://example.com/a","selector":null}""", Token);

        Assert.Equal(new HostMessage(HostMessage.OpenBackground, "https://example.com/a", null), message);
    }

    [Theory]
    [InlineData("""{"type":"open-window","url":"https://example.com/"}""")]                         // 페이지가 흉내 낸 메시지(토큰 없음)
    [InlineData("""{"type":"open-window","token":"WRONG","url":"https://example.com/"}""")]
    [InlineData("""{"type":"open-window","token":"SESSION","url":"javascript:alert(1)"}""")]
    [InlineData("""{"type":"open-window","token":"SESSION","url":"file:///C:/secret.txt"}""")]
    [InlineData("""{"type":"pick","token":"SESSION","selector":"a{} body{display:none"}""")]
    [InlineData("""["not","an","object"]""")]
    [InlineData("not json")]
    public void Parse_rejects_untrusted_or_unsafe_messages(string json)
        => Assert.Null(HostMessage.Parse(json, Token));
}
