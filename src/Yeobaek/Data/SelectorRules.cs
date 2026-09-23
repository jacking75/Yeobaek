namespace Yeobaek.Data;

/// <summary>저장할 숨김 선택자의 기본 검사. 실제 CSS 문법 검사는 주입 스크립트가 규칙마다 따로 한다.</summary>
public static class SelectorRules
{
    public const int MaxLength = 512;

    /// <summary>
    /// 선택자 뒤에 "{display:none}" 을 이어 붙여 규칙을 만들므로,
    /// 규칙을 닫거나 새로 여는 중괄호와 줄바꿈 같은 제어 문자는 받지 않는다.
    /// </summary>
    public static bool IsAcceptable(string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector) || selector.Length > MaxLength) return false;
        foreach (var ch in selector)
        {
            if (ch is '{' or '}' || char.IsControl(ch)) return false;
        }
        return true;
    }
}
