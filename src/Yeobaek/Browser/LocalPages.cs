using Yeobaek.Archive;
using Yeobaek.Reader;

namespace Yeobaek.Browser;

/// <summary>
/// 여백이 만든 로컬 페이지(리더 페이지·보관본). 광고 정리 도구를 쓰지 않고,
/// 주소창에는 가상 호스트 주소 대신 원문 주소를 보여 준다.
/// </summary>
public static class LocalPages
{
    public static bool IsLocal(string? url) => ReaderService.IsReaderUrl(url) || ArchiveService.IsArchiveUrl(url);
}
