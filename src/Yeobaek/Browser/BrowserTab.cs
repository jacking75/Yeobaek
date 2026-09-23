using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Yeobaek.Browser;

/// <summary>탭 하나. WebView2 컨트롤과, 화면(탭 줄·주소창·도구 모음)에 보여 줄 상태를 가진다.</summary>
public sealed class BrowserTab(WebView2 view) : INotifyPropertyChanged
{
    public const string NewTabTitle = "새 탭";

    public event PropertyChangedEventHandler? PropertyChanged;

    public WebView2 View { get; } = view;
    public CoreWebView2 Core => View.CoreWebView2;

    public string Title { get; private set => SetField(ref field, value); } = NewTabTitle;

    /// <summary>WebView 가 실제로 보고 있는 주소. 리더 모드·보관본이면 여백이 만든 로컬 페이지 주소다.</summary>
    public string? Url { get; private set => SetField(ref field, value); }

    /// <summary>리더 페이지·보관본을 보고 있을 때 원문 주소. 아니면 null.</summary>
    public string? ReaderSourceUrl { get; private set => SetField(ref field, value); }

    /// <summary>원문 대신 여백이 만든 읽기 화면(리더 페이지·보관본)을 보고 있는지.</summary>
    public bool IsReaderMode => ReaderSourceUrl is not null;

    /// <summary>주소창에 보여 줄 주소. 리더 모드에서도 원문 주소를 보여 준다.</summary>
    public string? DisplayUrl => ReaderSourceUrl ?? Url;

    public bool IsLoading { get; private set => SetField(ref field, value); }
    public bool CanGoBack { get; private set => SetField(ref field, value); }
    public bool CanGoForward { get; private set => SetField(ref field, value); }
    public bool IsFullScreen { get; private set => SetField(ref field, value); }

    /// <summary>한 번이라도 이동을 시작했는지. 아직이면 WebView 대신 새 탭 안내 화면을 보여 준다.</summary>
    public bool HasNavigated { get; private set => SetField(ref field, value); }

    /// <summary>지금 페이지에서 1층이 막은 요청 수.</summary>
    public int BlockedCount { get; private set => SetField(ref field, value); }

    /// <summary>CoreWebView2 이벤트를 상태에 연결한다.</summary>
    /// <param name="resolveSourceUrl">로컬 페이지 주소면 원문 주소, 아니면 null</param>
    internal void Bind(Func<string?, string?> resolveSourceUrl)
    {
        var core = Core;
        core.NavigationStarting += (_, _) =>
        {
            HasNavigated = true;
            IsLoading = true;
        };
        // 이동이 취소(차단)될 수도 있으므로 차단 수는 새 문서가 실제로 로드되기 시작할 때 비운다
        core.ContentLoading += (_, _) => BlockedCount = 0;
        core.NavigationCompleted += (_, _) => IsLoading = false;
        core.SourceChanged += (_, _) =>
        {
            Url = core.Source;
            ReaderSourceUrl = resolveSourceUrl(core.Source);
            OnPropertyChanged(nameof(IsReaderMode));
            OnPropertyChanged(nameof(DisplayUrl));
            UpdateTitle();
        };
        core.DocumentTitleChanged += (_, _) => UpdateTitle();
        core.HistoryChanged += (_, _) =>
        {
            CanGoBack = core.CanGoBack;
            CanGoForward = core.CanGoForward;
        };
        core.ContainsFullScreenElementChanged += (_, _) => IsFullScreen = core.ContainsFullScreenElement;
    }

    internal void CountBlockedRequest() => BlockedCount++;

    private void UpdateTitle()
    {
        var documentTitle = Core.DocumentTitle;
        Title = !string.IsNullOrWhiteSpace(documentTitle) ? documentTitle
              : DisplayUrl is { Length: > 0 } url && url != "about:blank" ? url
              : NewTabTitle;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
