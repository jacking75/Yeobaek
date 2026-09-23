namespace Yeobaek.Ui;

/// <summary>창 아래 알림 줄에 잠깐 띄우는 메시지. 동작 버튼(되돌리기·열기 등)을 하나 붙일 수 있다.</summary>
public sealed record Notice(string Message, bool IsError = false, string? ActionLabel = null, Action? Action = null)
{
    public static Notice Info(string message, string? actionLabel = null, Action? action = null)
        => new(message, false, actionLabel, action);

    public static Notice Error(string message, string? actionLabel = null, Action? action = null)
        => new(message, true, actionLabel, action);
}
