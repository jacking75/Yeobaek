using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Yeobaek.Data;

public sealed class AppSettings
{
    public const string DefaultSearchUrl = "https://www.google.com/search?q={0}";

    /// <summary>새 창·새 탭이 처음 여는 주소. http/https 가 아니면 빈 탭으로 연다.</summary>
    public string StartPage { get; set; } = "about:blank";

    /// <summary>주소창에 검색어를 넣었을 때 쓸 주소. {0} 자리에 검색어가 들어간다.</summary>
    public string SearchUrl { get; set; } = DefaultSearchUrl;

    public ReaderSettings Reader { get; set; } = new();
}

public sealed class ReaderSettings
{
    public int FontSizePx { get; set; } = 17;
    public double LineHeight { get; set; } = 1.85;
    public double MaxWidthRem { get; set; } = 42;
    public string FontFamily { get; set; } = "\"Pretendard\", \"Malgun Gothic\", system-ui, sans-serif";

    /// <summary>system(시스템 설정 따름) · light · dark</summary>
    public string Theme { get; set; } = "system";
}

/// <summary>
/// settings.json. 없으면 기본값으로 만들어 두어 사용자가 직접 고칠 수 있게 하고,
/// 파일이 바뀌면 다시 읽는다. 읽을 수 없으면 파일을 덮어쓰지 않고 기본값을 쓴다.
/// </summary>
public sealed class SettingsFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 사람이 읽고 고치는 파일이라 한글·따옴표를 그대로 쓴다
    };

    private readonly string _path;
    private DateTime _lastWriteTimeUtc;

    public AppSettings Current { get; private set; } = new();

    /// <summary>마지막으로 읽을 때 난 문제. 없으면 null.</summary>
    public string? Error { get; private set; }

    public SettingsFile(string path)
    {
        _path = path;
        Load();
    }

    /// <summary>파일이 바뀌었으면 다시 읽고 true 를 돌려준다.</summary>
    public bool ReloadIfChanged()
    {
        if (File.GetLastWriteTimeUtc(_path) == _lastWriteTimeUtc) return false;
        Load();
        return true;
    }

    private void Load()
    {
        Error = null;
        try
        {
            if (!File.Exists(_path))
            {
                Current = new AppSettings();
                File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
            }
            else
            {
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Current = new AppSettings();
            Error = $"settings.json 을 읽지 못해 기본 설정을 씁니다. 파일 내용을 확인해 주세요. ({ex.Message})";
        }
        _lastWriteTimeUtc = File.GetLastWriteTimeUtc(_path);
    }
}
