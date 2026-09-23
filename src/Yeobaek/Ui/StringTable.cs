using System.Globalization;
using System.Text.Json;

namespace Yeobaek.Ui;

/// <summary>프로그램에 내장한 언어별 문자열 표. 빠진 번역은 한국어로 표시한다.</summary>
public static class StringTable
{
    public static readonly string[] Languages = ["ko", "en", "ja", "zh-CN"];

    private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> Entries = LoadTable();

    public static string Language { get; private set; } = "ko";

    public static string NormalizeLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" or "en-us" => "en",
        "ja" or "ja-jp" => "ja",
        "zh" or "zh-cn" or "zh-hans" => "zh-CN",
        _ => "ko",
    };

    public static void SetLanguage(string? language) => Language = NormalizeLanguage(language);

    public static string Get(string key) => Get(Language, key);

    public static string Get(string? language, string key)
    {
        if (!Entries.TryGetValue(key, out var translations)) return key;
        var selected = NormalizeLanguage(language);
        if (translations.TryGetValue(selected, out var value)) return value;
        return translations.TryGetValue("ko", out var korean) ? korean : key;
    }

    public static string Format(string key, params object?[] args)
    {
        var culture = CultureInfo.GetCultureInfo(Language switch
        {
            "en" => "en-US",
            "ja" => "ja-JP",
            "zh-CN" => "zh-CN",
            _ => "ko-KR",
        });
        return string.Format(culture, Get(key), args);
    }

    public static IReadOnlyDictionary<string, Dictionary<string, string>> GetEntries() => Entries;

    private static IReadOnlyDictionary<string, Dictionary<string, string>> LoadTable()
    {
        using var stream = typeof(StringTable).Assembly.GetManifestResourceStream("Yeobaek.Strings.strings.json")
            ?? throw new InvalidOperationException("Missing string table: strings.json");
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new InvalidOperationException("Empty string table: strings.json");
    }
}
