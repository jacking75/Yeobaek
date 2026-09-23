using System.IO;
using Yeobaek.Data;

namespace Yeobaek;

/// <summary>예상하지 못한 오류를 error.log 에 남긴다. 로그 쓰기 실패가 앱을 멈추게 하지 않는다.</summary>
public static class AppLog
{
    private const long MaxLogBytes = 1024 * 1024;

    public static void Error(Exception exception, string context)
    {
        try
        {
            var path = AppPaths.ErrorLog;
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes) File.Delete(path);
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // 로그를 남길 수 없는 상황(디스크 가득 참 등)에서는 조용히 넘어간다
        }
    }
}
