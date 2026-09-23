using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek.Browser;

/// <summary>
/// 창 하나의 탭 목록. 탭을 만들고 서비스를 붙이며, 전환·닫기·복원을 맡는다.
/// 모든 탭의 WebView2 는 host 패널에 함께 올려 두고 보이기만 바꾼다.
/// (WPF WebView2 는 창 핸들이 생겨야 초기화가 끝나므로 배경 탭도 시각 트리에 있어야 하고,
///  시각 트리에서 뺐다 넣으면 페이지가 다시 로드된다.)
/// </summary>
public sealed class TabManager(AppServices services, Panel host)
{
    private const int SuspendThreshold = 15;
    private const int RecentlyClosedLimit = 25;

    private readonly List<string> _recentlyClosed = [];

    public AppServices Services { get; } = services;
    public ObservableCollection<BrowserTab> Tabs { get; } = [];
    public BrowserTab? Active { get; private set; }

    public event Action? ActiveChanged;
    public event Action? AllTabsClosed;
    public event Action<Notice>? NoticeRequested;

    public void Notify(Notice notice) => NoticeRequested?.Invoke(notice);

    /// <summary>
    /// 기다릴 곳이 없는 호출(페이지 메시지·메뉴·단축키)용 탭 열기. 실패하면 알림으로 알린다.
    /// </summary>
    public async void OpenTab(string? url, bool activate)
    {
        try
        {
            await CreateTabAsync(url, activate);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, StringTable.Get("Log.CreateTab"));
            Notify(Notice.Error(StringTable.Get("Browser.NewTabFailed")));
        }
    }

    /// <summary>탭 하나를 만들고 모든 서비스를 붙인다. url 이 null 이면 이동하지 않는다.</summary>
    public async Task<BrowserTab> CreateTabAsync(string? url, bool activate)
    {
        var tab = await PrepareTabAsync(activate);
        CompleteTab(tab);
        if (url is not null) tab.Core.Navigate(url);
        return tab;
    }

    /// <summary>
    /// 1단계: WebView2 초기화, 설정, 문서 스크립트 등록까지. NewWindowRequested 에서는 이 상태의 탭을
    /// e.NewWindow 에 대입한 뒤 <see cref="CompleteTab"/> 을 부른다(WebView2 가 요구하는 순서).
    /// </summary>
    internal async Task<BrowserTab> PrepareTabAsync(bool activate)
    {
        var view = new WebView2 { Visibility = Visibility.Hidden };
        host.Children.Add(view);
        try
        {
            await view.EnsureCoreWebView2Async(Services.Environment);   // 공유 환경 필수
            ConfigureSettings(view.CoreWebView2);
            await Services.Scripts.AttachAsync(view.CoreWebView2);
        }
        catch
        {
            if (view.CoreWebView2 is { } core) Services.Scripts.Detach(core);
            host.Children.Remove(view);
            view.Dispose();
            throw;
        }

        var tab = new BrowserTab(view);
        Tabs.Add(tab);
        if (activate || Active is null) Activate(tab);
        return tab;
    }

    /// <summary>2단계: 네트워크 차단, 리더 호스트, 이벤트 연결. NewWindow 대입 뒤에 불러야 한다.</summary>
    internal void CompleteTab(BrowserTab tab)
    {
        var core = tab.Core;
        // 리더 페이지·보관본이면 주소창에 원문 주소를 보여 준다
        tab.Bind(url => Services.Reader.GetSourceUrl(url) ?? Services.Archive.GetSourceUrl(url));
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BrowserTab.HasNavigated)) UpdateVisibility();
        };

        Services.Reader.Attach(core);
        Services.Archive.Attach(core);
        Services.Blocker.Attach(core, tab.CountBlockedRequest,
            url => Notify(Notice.Info(StringTable.Format("Browser.BlockedAddress", HostName.FromUrl(url) ?? url))));
        new ContextMenuService(this, tab).Attach();
        new HostMessageBridge(this, tab).Attach();
        new PopupHandler(this, tab).Attach();

        // 이벤트 처리 중에 자기 WebView 를 해제하지 않도록 한 박자 미룬다
        core.WindowCloseRequested += (_, _) => tab.View.Dispatcher.InvokeAsync(() => Close(tab));
        core.ProcessFailed += (_, e) => OnProcessFailed(tab, e);
    }

    public void Activate(BrowserTab tab)
    {
        if (ReferenceEquals(Active, tab)) return;
        Active = tab;
        UpdateVisibility();
        ActiveChanged?.Invoke();
        SuspendInactiveTabsIfCrowded();
    }

    /// <summary>규칙·예외 사이트를 바꾼 뒤, 새 문서 스크립트가 등록되고 나서 탭을 새로 고친다.</summary>
    public async void ReloadWhenRulesApplied(BrowserTab tab)
    {
        try
        {
            await Services.Scripts.WhenUpdatedAsync();
            if (Tabs.Contains(tab) && !tab.IsReaderMode) tab.Core.Reload();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, StringTable.Get("Log.ReloadRules"));
        }
    }

    /// <summary>step 만큼 옆 탭으로 옮긴다(끝에서는 반대쪽으로 돈다).</summary>
    public void ActivateNeighbor(int step)
    {
        if (Active is null || Tabs.Count < 2) return;
        var index = (Tabs.IndexOf(Active) + step + Tabs.Count) % Tabs.Count;
        Activate(Tabs[index]);
    }

    public void Close(BrowserTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        if (SafeUrl.IsWebUrl(tab.DisplayUrl)) Remember(tab.DisplayUrl);
        Tabs.RemoveAt(index);
        Release(tab);

        if (Tabs.Count == 0)
        {
            Active = null;
            AllTabsClosed?.Invoke();
            return;
        }
        if (ReferenceEquals(Active, tab))
        {
            Active = null;
            Activate(Tabs[Math.Min(index, Tabs.Count - 1)]);
        }
    }

    /// <summary>창을 닫을 때. 닫은 탭 기록은 남기지 않는다.</summary>
    public void CloseAll()
    {
        foreach (var tab in Tabs.ToList()) Release(tab);
        Tabs.Clear();
        Active = null;
    }

    /// <summary>가장 최근에 닫은 탭을 다시 연다. 다시 열 탭이 없으면 false.</summary>
    public bool ReopenClosed()
    {
        if (_recentlyClosed.Count == 0) return false;
        var url = _recentlyClosed[^1];
        _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
        OpenTab(url, activate: true);
        return true;
    }

    private static void ConfigureSettings(CoreWebView2 core)
    {
        var settings = core.Settings;
        // 링크 우클릭 메뉴를 쓰려면 반드시 true 여야 한다. false 면 ContextMenuRequested 가 아예 발생하지 않는다.
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.AreDevToolsEnabled = true;   // 선택자 확인용
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
    }

    private void UpdateVisibility()
    {
        foreach (var tab in Tabs)
        {
            var visible = ReferenceEquals(tab, Active) && tab.HasNavigated;
            tab.View.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private void Remember(string url)
    {
        _recentlyClosed.Add(url);
        if (_recentlyClosed.Count > RecentlyClosedLimit) _recentlyClosed.RemoveAt(0);
    }

    private void Release(BrowserTab tab)
    {
        if (tab.View.CoreWebView2 is { } core)
        {
            Services.Blocker.Detach(core);
            Services.Scripts.Detach(core);
        }
        host.Children.Remove(tab.View);
        tab.View.Dispose();   // 명시적 해제. 안 하면 브라우저 프로세스가 남는다
    }

    /// <summary>탭이 많으면 보이지 않는 탭을 절전시켜 메모리를 아낀다. 다시 보이면 WebView2 가 알아서 깨운다.</summary>
    private void SuspendInactiveTabsIfCrowded()
    {
        if (Tabs.Count < SuspendThreshold) return;
        // 숨김 상태가 컨트롤러의 IsVisible 에 반영된 뒤에 재워야 한다(보이는 탭은 예외를 던진다)
        host.Dispatcher.InvokeAsync(async () =>
        {
            foreach (var tab in Tabs.ToList())
            {
                if (!ReferenceEquals(tab, Active)) await TrySuspendAsync(tab);
            }
        }, DispatcherPriority.Background);
    }

    private static async Task TrySuspendAsync(BrowserTab tab)
    {
        try
        {
            var core = tab.View.CoreWebView2;
            if (core is null || core.IsSuspended) return;
            await core.TrySuspendAsync();
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // 방금 보이게 됐거나 닫힌 탭(ObjectDisposedException 포함)이다. 건너뛴다.
        }
    }

    private void OnProcessFailed(BrowserTab tab, CoreWebView2ProcessFailedEventArgs e)
    {
        switch (e.ProcessFailedKind)
        {
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                Notify(Notice.Error(StringTable.Get("Browser.EngineExited")));
                break;
            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                Notify(Notice.Error(StringTable.Format("Browser.TabUnresponsive", tab.Title), StringTable.Get("Main.Reload"), () => tab.Core.Reload()));
                break;
        }
    }
}
