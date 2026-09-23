using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Yeobaek.Archive;
using Yeobaek.Browser;
using Yeobaek.Data;
using Yeobaek.Reader;
using Yeobaek.Ui;

namespace Yeobaek;

// 단축키·메뉴·도구 모음 명령.
public partial class MainWindow
{
    private bool _readerBusy;
    private bool _archiveBusy;

    /// <summary>
    /// 창 단축키. WebView2 에 초점이 있어도 가속키(Ctrl·Alt 조합, F 키)는 WPF 키 이벤트로 먼저 온다.
    /// 여기서 Handled 로 표시하면 브라우저 기본 동작(예: Ctrl+R 새로 고침)은 일어나지 않는다.
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var command = CommandFor(key, Keyboard.Modifiers);
        if (command is null) return;

        e.Handled = true;
        // WebView2 가 넘겨준 가속키를 처리하는 동안에는 브라우저 프로세스가 멈춰 있어
        // WebView2 API 를 부르면 실패할 수 있다. 큐로 미뤄 처리가 끝난 뒤 실행한다.
        Dispatcher.InvokeAsync(command);
    }

    private Action? CommandFor(Key key, ModifierKeys modifiers) => (modifiers, key) switch
    {
        (ModifierKeys.Control, Key.T) => NewTab,
        (ModifierKeys.Control, Key.N) => () => MainWindow.Open(_services, App.StartPageOf(_services)),
        (ModifierKeys.Control, Key.W) => CloseActiveTab,
        (ModifierKeys.Control, Key.Tab) => () => _tabs.ActivateNeighbor(+1),
        (ModifierKeys.Control | ModifierKeys.Shift, Key.Tab) => () => _tabs.ActivateNeighbor(-1),
        (ModifierKeys.Control | ModifierKeys.Shift, Key.T) => ReopenClosedTab,
        (ModifierKeys.Control, Key.R) => () => _ = ToggleReaderAsync(),
        (ModifierKeys.Control, Key.D) => AddActiveTabToQueue,
        (ModifierKeys.Control, Key.S) => () => _ = ArchiveActiveTabAsync(),
        (ModifierKeys.Control | ModifierKeys.Shift, Key.F) => ShowArchive,
        (ModifierKeys.Control, Key.L) => FocusAddressBar,
        (ModifierKeys.Alt | ModifierKeys.Shift, Key.Z) => StartPicker,
        (ModifierKeys.Alt | ModifierKeys.Shift, Key.A) => ToggleAllowlist,
        (ModifierKeys.Alt, Key.Left) => GoBack,
        (ModifierKeys.Alt, Key.Right) => GoForward,
        (ModifierKeys.None, Key.F5) => Reload,
        _ => null,
    };

    // ── 명령 ──────────────────────────────────────────────────

    private void NewTab() => _tabs.OpenTab(App.StartPageOf(_services), activate: true);

    private void CloseActiveTab()
    {
        if (_tabs.Active is { } tab) _tabs.Close(tab);
    }

    private void ReopenClosedTab()
    {
        if (!_tabs.ReopenClosed()) ShowNotice(Notice.Info("다시 열 탭이 없습니다."));
    }

    private void GoBack()
    {
        if (_tabs.Active is { CanGoBack: true } tab) tab.Core.GoBack();
    }

    private void GoForward()
    {
        if (_tabs.Active is { CanGoForward: true } tab) tab.Core.GoForward();
    }

    private void Reload()
    {
        if (_tabs.Active is { HasNavigated: true } tab) tab.Core.Reload();
    }

    /// <summary>
    /// 리더 모드 토글. 본문을 찾지 못하는 페이지(SPA·로그인 필요·이미지 위주)에서는
    /// 원본(1·2층만 적용된 화면)을 그대로 두고 알린다.
    /// </summary>
    private async Task ToggleReaderAsync()
    {
        var tab = _tabs.Active;
        if (tab is null || _readerBusy) return;

        if (tab.IsReaderMode)
        {
            tab.Core.Navigate(tab.ReaderSourceUrl!);
            return;
        }
        if (!SafeUrl.IsWebUrl(tab.Url))
        {
            ShowNotice(Notice.Info("웹 페이지에서만 리더 모드를 쓸 수 있습니다."));
            return;
        }

        _readerBusy = true;
        UpdateReaderButton();
        var sourceUrl = tab.Url;
        try
        {
            var readerUrl = await _services.Reader.CreateReaderPageAsync(tab.Core);
            if (readerUrl is null)
                ShowNotice(Notice.Info("이 페이지에서는 본문을 찾지 못해 원래 화면을 그대로 둡니다."));
            else if (tab.Url == sourceUrl)   // 추출하는 사이에 다른 곳으로 이동했다면 덮어쓰지 않는다
                tab.Core.Navigate(readerUrl);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "리더 모드");
            ShowNotice(Notice.Error("본문을 추출하다 문제가 생겼습니다. 원래 화면을 그대로 둡니다."));
        }
        finally
        {
            _readerBusy = false;
            UpdateReaderButton();
        }
    }

    private void AddActiveTabToQueue()
    {
        var tab = _tabs.Active;
        if (tab is null || !SafeUrl.IsWebUrl(tab.DisplayUrl))
        {
            ShowNotice(Notice.Info("웹 페이지만 나중에 읽기에 담을 수 있습니다."));
            return;
        }
        var added = _services.Queue.Add(tab.DisplayUrl, tab.Title);
        ShowNotice(Notice.Info(added ? "나중에 읽기에 추가했습니다." : "이미 나중에 읽기 목록에 있습니다.", "목록 보기", ShowQueue));
    }

    private void StartPicker()
    {
        var tab = _tabs.Active;
        if (tab is null || !SafeUrl.IsWebUrl(tab.Url) || tab.IsReaderMode)
        {
            ShowNotice(Notice.Info("웹 페이지에서만 광고 요소를 고를 수 있습니다."));
            return;
        }
        _ = tab.Core.ExecuteScriptAsync("window.__yeobaekPick && window.__yeobaekPick(false)");
        tab.View.Focus();   // Esc·방향키가 페이지로 가도록
    }

    /// <summary>즐겨 읽어 후원하고 싶은 사이트를 차단 예외로 두거나 다시 차단한다.</summary>
    private void ToggleAllowlist()
    {
        var tab = _tabs.Active;
        var host = HostName.FromUrl(tab?.DisplayUrl);
        if (tab is null || host is null)
        {
            ShowNotice(Notice.Info("웹 페이지에서만 쓸 수 있습니다."));
            return;
        }

        var rules = _services.Rules;
        if (rules.FindAllowlistEntry(host) is { } entry)
        {
            rules.SetAllowlisted(entry, false);
            ShowNotice(Notice.Info($"{entry} 의 광고를 다시 차단합니다.", "되돌리기", () => SetAllowlistedAndReload(tab, entry, true)));
        }
        else
        {
            rules.SetAllowlisted(host, true);
            ShowNotice(Notice.Info($"{host} 을 예외 사이트로 두었습니다. 이 사이트에서는 광고를 차단하지 않습니다.",
                "되돌리기", () => SetAllowlistedAndReload(tab, host, false)));
        }
        _tabs.ReloadWhenRulesApplied(tab);
    }

    private void SetAllowlistedAndReload(BrowserTab tab, string host, bool allowed)
    {
        _services.Rules.SetAllowlisted(host, allowed);
        _tabs.ReloadWhenRulesApplied(tab);
    }

    /// <summary>
    /// 지금 글을 오프라인 보관한다. 리더 모드면 이미 추출한 본문을, 아니면 지금 DOM 에서 새로 추출한다.
    /// 이미지를 내려받느라 몇 초 걸릴 수 있어 진행 중임을 알린다.
    /// </summary>
    private async Task ArchiveActiveTabAsync()
    {
        var tab = _tabs.Active;
        if (tab is null) return;
        if (_archiveBusy)
        {
            ShowNotice(Notice.Info("앞의 글을 보관하는 중입니다. 끝난 뒤 다시 시도해 주세요."));
            return;
        }
        if (ArchiveService.IsArchiveUrl(tab.Url))
        {
            ShowNotice(Notice.Info("이미 보관한 글을 보고 있습니다.", "보관함 열기", ShowArchive));
            return;
        }
        var sourceUrl = tab.DisplayUrl;
        if (!SafeUrl.IsWebUrl(sourceUrl))
        {
            ShowNotice(Notice.Info("웹 페이지만 보관할 수 있습니다."));
            return;
        }

        _archiveBusy = true;
        ShowNotice(Notice.Info("보관하는 중입니다. 본문과 이미지를 PC 에 저장하고 있어요…"));
        try
        {
            var userAgent = tab.Core.Settings.UserAgent;   // 기다리는 사이 탭이 닫힐 수 있어 먼저 읽는다
            var article = tab.IsReaderMode
                ? _services.Reader.GetRecentArticle(sourceUrl)
                : await ArticleExtractor.ExtractAsync(tab.Core);
            if (article is null)
            {
                ShowNotice(Notice.Info(tab.IsReaderMode
                    ? "리더 모드를 끄고 원문에서 다시 보관해 주세요."
                    : "이 페이지에서는 본문을 찾지 못해 보관하지 못했습니다."));
                return;
            }

            var result = await _services.Archive.SaveAsync(article, userAgent);
            var missing = result.ImagesTotal - result.ImagesSaved;
            var message = $"보관했습니다 · {result.Article.SizeLabel}"
                          + (result.ImagesTotal > 0 ? $" · 이미지 {result.ImagesSaved}/{result.ImagesTotal}개" : "")
                          + (missing > 0 ? $" (저장하지 못한 {missing}개는 인터넷이 있을 때만 보입니다)" : "");
            ShowNotice(Notice.Info(message, "보관함 열기", ShowArchive));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "글 보관");
            ShowNotice(Notice.Error("보관하지 못했습니다. 보관본을 연 탭이 있다면 닫고 다시 시도해 주세요."));
        }
        finally
        {
            _archiveBusy = false;
        }
    }

    private void ShowArchive() => ArchiveWindow.ShowSingle(_services, OpenFromQueue);

    private void ShowQueue() => ReadingQueueWindow.ShowSingle(_services, OpenFromQueue);

    /// <summary>목록 창(나중에 읽기·보관함)보다 이 창이 먼저 닫혔으면 새 창에서 연다.</summary>
    private void OpenFromQueue(string url)
    {
        if (_isClosed) MainWindow.Open(_services, url);
        else _tabs.OpenTab(url, activate: true);
    }

    private void ShowRuleEditor()
    {
        var host = HostName.FromUrl(_tabs.Active?.DisplayUrl);
        RuleEditorWindow.ShowFor(host ?? "", _services);
    }

    private void OpenWithShell(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowNotice(Notice.Error($"열지 못했습니다: {path}"));
        }
    }

    // ── 클릭 처리기 ───────────────────────────────────────────

    private void NewTab_Click(object sender, RoutedEventArgs e) => NewTab();
    private void NewWindow_Click(object sender, RoutedEventArgs e) => MainWindow.Open(_services, App.StartPageOf(_services));
    private void ReopenTab_Click(object sender, RoutedEventArgs e) => ReopenClosedTab();
    private void Back_Click(object sender, RoutedEventArgs e) => GoBack();
    private void Forward_Click(object sender, RoutedEventArgs e) => GoForward();
    private void Reload_Click(object sender, RoutedEventArgs e) => Reload();
    private void Reader_Click(object sender, RoutedEventArgs e) => _ = ToggleReaderAsync();
    private void AddToQueue_Click(object sender, RoutedEventArgs e) => AddActiveTabToQueue();
    private void ShowQueue_Click(object sender, RoutedEventArgs e) => ShowQueue();
    private void Archive_Click(object sender, RoutedEventArgs e) => _ = ArchiveActiveTabAsync();
    private void ShowArchive_Click(object sender, RoutedEventArgs e) => ShowArchive();
    private void Pick_Click(object sender, RoutedEventArgs e) => StartPicker();
    private void ManageRules_Click(object sender, RoutedEventArgs e) => ShowRuleEditor();
    private void ToggleAllowlist_Click(object sender, RoutedEventArgs e) => ToggleAllowlist();
    private void EditBlockList_Click(object sender, RoutedEventArgs e) => OpenWithShell(AppPaths.BlockList);
    private void EditSettings_Click(object sender, RoutedEventArgs e) => OpenWithShell(AppPaths.Settings);
    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenWithShell(AppPaths.Root);

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        AppMenu.PlacementTarget = MenuButton;
        AppMenu.Placement = PlacementMode.Bottom;
        AppMenu.IsOpen = true;
    }

    private void AppMenu_Opened(object sender, RoutedEventArgs e)
    {
        var host = HostName.FromUrl(_tabs.Active?.DisplayUrl);
        AllowlistMenuItem.IsEnabled = host is not null;
        AllowlistMenuItem.IsChecked = host is not null && _services.Rules.IsAllowlisted(host);
    }
}
