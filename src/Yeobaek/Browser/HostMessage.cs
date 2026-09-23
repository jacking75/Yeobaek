using System.Text.Json;
using Yeobaek.Data;

namespace Yeobaek.Browser;

/// <summary>
/// 주입 스크립트(prelude.js · picker.js)가 postMessage 로 보내는 메시지.
/// 페이지 스크립트도 postMessage 를 부를 수 있으므로 모든 값을 신뢰하지 않는 입력으로 다룬다.
/// 주입 스크립트만 아는 세션 토큰이 없으면 버리고, URL 은 http/https 만 받는다.
/// </summary>
public sealed record HostMessage(string Type, string? Url, string? Selector)
{
    public const string OpenBackground = "open-background";
    public const string OpenForeground = "open-foreground";
    public const string OpenWindow = "open-window";
    public const string Pick = "pick";

    /// <summary>올바른 메시지면 돌려주고, 아니면 null.</summary>
    public static HostMessage? Parse(string json, string expectedToken)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (ReadString(root, "token") != expectedToken) return null;

            var type = ReadString(root, "type");
            var url = ReadString(root, "url");
            var selector = ReadString(root, "selector");

            return type switch
            {
                OpenBackground or OpenForeground or OpenWindow when SafeUrl.IsWebUrl(url) => new HostMessage(type, url, null),
                Pick when SelectorRules.IsAcceptable(selector) => new HostMessage(type, null, selector),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
