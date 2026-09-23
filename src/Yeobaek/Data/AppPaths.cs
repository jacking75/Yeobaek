using System.IO;

namespace Yeobaek.Data;

/// <summary>
/// 앱 데이터 폴더(%LOCALAPPDATA%\Yeobaek) 안의 경로. 모든 상태는 이 폴더의 파일로만 보관한다.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = EnsureDir(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Yeobaek"));

    public static string RulesDb => Path.Combine(Root, "rules.db");
    public static string BlockList => Path.Combine(Root, "blocklist.txt");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string ErrorLog => Path.Combine(Root, "error.log");
    public static string Profile => EnsureDir(Path.Combine(Root, "WebView2"));

    /// <summary>리더 모드 페이지 파일. 가상 호스트로 매핑해 서빙한다.</summary>
    public static string ReaderCache => EnsureDir(Path.Combine(Root, "reader"));

    /// <summary>보관한 글(HTML·이미지). 지우지 않는 한 남는다. 가상 호스트로 매핑해 서빙한다.</summary>
    public static string Archive => EnsureDir(Path.Combine(Root, "archive"));

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
