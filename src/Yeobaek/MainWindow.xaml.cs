using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Yeobaek.Browser;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek;

/// <summary>
/// 브라우저 창. 탭 줄·도구 모음·알림 줄을 그리고 활성 탭의 상태를 화면에 반영한다.
/// 명령(단축키·메뉴)은 MainWindow.Commands.cs, 알림 줄은 MainWindow.Notice.cs 에 있다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppServices _services;
    private readonly TabManager _tabs;
    private BrowserTab? _observedTab;
    private bool _syncingSelection;
    private bool _settingAddressText;
    private bool _addressEdited;   // 사용자가 주소창에 직접 입력한 글자가 있는지
    private bool _isFullScreen;
    private bool _isClosed;
    private WindowState _stateBeforeFullScreen;

    public MainWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _tabs = new TabManager(services, TabHost);
        _tabs.ActiveChanged += OnActiveTabChanged;
        _tabs.AllTabsClosed += Close;
        _tabs.NoticeRequested += ShowNotice;
        TabStrip.ItemsSource = _tabs.Tabs;

        _noticeTimer.Tick += (_, _) => HideNotice();
        services.Rules.AllowlistChanged += UpdateShield;
        Activated += OnWindowActivated;
        Closed += OnWindowClosed;
    }

    /// <summary>새 창을 띄우고 url 을 첫 탭으로 연다. url 이 null 이면 빈 탭으로 연다.</summary>
    public static MainWindow Open(AppServices services, string? url)
    {
        var window = new MainWindow(services);
        window.Show();   // WebView2 초기화에는 창 핸들이 필요하므로 먼저 띄운다
        window._tabs.OpenTab(url, activate: true);
        return window;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (_services.ReloadChangedFiles() is { } message) ShowNotice(Notice.Info(message));
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _services.Rules.AllowlistChanged -= UpdateShield;
        _noticeTimer.Stop();
        _tabs.CloseAll();

        // 마지막 브라우저 창이면 보조 창(나중에 읽기·규칙 관리)도 닫아 앱이 끝나게 한다
        var windows = Application.Current.Windows.OfType<Window>().Where(window => window != this).ToList();
        if (!windows.OfType<MainWindow>().Any())
        {
            foreach (var window in windows) window.Close();
        }
    }

    // ── 활성 탭 반영 ──────────────────────────────────────────

    private void OnActiveTabChanged()
    {
        if (_observedTab is not null) _observedTab.PropertyChanged -= OnActiveTabPropertyChanged;
        _observedTab = _tabs.Active;
        if (_observedTab is not null) _observedTab.PropertyChanged += OnActiveTabPropertyChanged;

        _syncingSelection = true;
        TabStrip.SelectedItem = _observedTab;
        _syncingSelection = false;

        UpdateAll();
        FocusActiveContent();
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BrowserTab.BlockedCount))
        {
            UpdateShield();
            return;
        }
        UpdateAll();
        // 팝업·시작 페이지처럼 스스로 이동을 시작한 새 탭은 주소창 대신 페이지에 초점을 준다
        if (e.PropertyName == nameof(BrowserTab.HasNavigated) && !_addressEdited) FocusActiveContent();
    }

    private void UpdateAll()
    {
        var tab = _tabs.Active;
        Title = tab is null || tab.Title == BrowserTab.NewTabTitle ? StringTable.Get("App.Name") : StringTable.Format("Main.TitleWithPage", tab.Title);
        UpdateAddressBar(force: false);

        BackButton.IsEnabled = tab?.CanGoBack == true;
        ForwardButton.IsEnabled = tab?.CanGoForward == true;
        ReloadButton.IsEnabled = tab?.HasNavigated == true;
        LoadingBar.Visibility = tab?.IsLoading == true ? Visibility.Visible : Visibility.Hidden;
        UpdateReaderButton();
        UpdateShield();

        var showNewTabPanel = tab is not null && !tab.HasNavigated;
        NewTabPanel.Visibility = showNewTabPanel ? Visibility.Visible : Visibility.Collapsed;
        if (showNewTabPanel) UpdateNewTabQueueButton();

        SetFullScreen(tab?.IsFullScreen == true);
    }

    private void UpdateAddressBar(bool force)
    {
        if (!force && _addressEdited && AddressBar.IsKeyboardFocused) return;   // 입력 중인 글자를 덮어쓰지 않는다

        var url = _tabs.Active?.DisplayUrl;
        _settingAddressText = true;
        AddressBar.Text = url is null || url == "about:blank" ? "" : url;
        _settingAddressText = false;
        _addressEdited = false;
    }

    private void UpdateReaderButton()
    {
        var tab = _tabs.Active;
        ReaderButton.IsChecked = tab?.IsReaderMode == true;
        ReaderButton.IsEnabled = !_readerBusy && tab is not null && (tab.IsReaderMode || SafeUrl.IsWebUrl(tab.Url));
    }

    private void UpdateShield()
    {
        var tab = _tabs.Active;
        var host = HostName.FromUrl(tab?.DisplayUrl);
        if (tab is null || host is null || tab.IsReaderMode)
        {
            ShieldButton.Visibility = Visibility.Collapsed;
            return;
        }

        ShieldButton.Visibility = Visibility.Visible;
        if (_services.Rules.FindAllowlistEntry(host) is { } entry)
        {
            ShieldText.Text = StringTable.Get("Main.ExceptionSite");
            ShieldButton.Background = (System.Windows.Media.Brush)FindResource("WarnSoftBrush");
            ShieldButton.Foreground = (System.Windows.Media.Brush)FindResource("WarnTextBrush");
            ShieldButton.ToolTip = StringTable.Format("Main.ExceptionTip", entry);
        }
        else
        {
            ShieldText.Text = StringTable.Format("Main.BlockedCount", tab.BlockedCount);
            ShieldButton.Background = (System.Windows.Media.Brush)FindResource("AccentSoftBrush");
            ShieldButton.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
            ShieldButton.ToolTip = StringTable.Format("Main.BlockedTip", tab.BlockedCount);
        }
    }

    private void UpdateNewTabQueueButton()
    {
        var unread = _services.Queue.CountUnread();
        NewTabQueueButton.Content = unread > 0 ? StringTable.Format("Main.UnreadCount", unread) : StringTable.Get("Main.QueueName");
        var (archived, _) = _services.Articles.Summary();
        NewTabArchiveButton.Content = archived > 0 ? StringTable.Format("Main.ArchivedCount", archived) : StringTable.Get("Main.ArchiveName");
    }

    private void FocusActiveContent()
    {
        var tab = _tabs.Active;
        if (tab is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!ReferenceEquals(tab, _tabs.Active)) return;
            if (tab.HasNavigated) tab.View.Focus();
            else FocusAddressBar();
        }, DispatcherPriority.Input);
    }

    /// <summary>페이지가 동영상 등을 전체 화면으로 띄우면 창도 테두리·도구 모음 없이 화면을 채운다.</summary>
    private void SetFullScreen(bool fullScreen)
    {
        if (fullScreen == _isFullScreen) return;
        _isFullScreen = fullScreen;

        var chrome = fullScreen ? Visibility.Collapsed : Visibility.Visible;
        TabStripRow.Visibility = chrome;
        ToolbarRow.Visibility = chrome;
        if (fullScreen)
        {
            _stateBeforeFullScreen = WindowState;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;      // 최대화 상태에서 테두리를 없애면 작업 표시줄을 덮지 못해 한 번 풀었다가 다시 최대화한다
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _stateBeforeFullScreen;
        }
    }

    // ── 탭 줄 ─────────────────────────────────────────────────

    private void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection) return;
        if (TabStrip.SelectedItem is BrowserTab tab) _tabs.Activate(tab);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BrowserTab tab) _tabs.Close(tab);
    }

    private void TabItem_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        if ((sender as FrameworkElement)?.DataContext is BrowserTab tab)
        {
            e.Handled = true;
            _tabs.Close(tab);
        }
    }

    // ── 주소창 ────────────────────────────────────────────────

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            NavigateFromAddressBar();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            UpdateAddressBar(force: true);
            FocusActiveContent();
        }
    }

    private void NavigateFromAddressBar()
    {
        var url = AddressInput.Resolve(AddressBar.Text, _services.Settings.Current.SearchUrl);
        if (url is null) return;
        _addressEdited = false;

        var tab = _tabs.Active;
        if (tab is null)
        {
            _tabs.OpenTab(url, activate: true);
            return;
        }
        try
        {
            tab.Core.Navigate(url);
            tab.View.Focus();
        }
        catch (ArgumentException)
        {
            ShowNotice(Notice.Error(StringTable.Get("Main.InvalidAddress")));
        }
    }

    /// <summary>처음 누를 때는 전체 선택만 한다(주소를 통째로 바꾸기 쉽게).</summary>
    private void AddressBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (AddressBar.IsKeyboardFocusWithin) return;
        e.Handled = true;
        FocusAddressBar();
    }

    private void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_settingAddressText) _addressEdited = true;
    }

    private void AddressBar_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => AddressBar.SelectAll();

    private void AddressBar_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateAddressBar(force: true);

    private void FocusAddressBar()
    {
        AddressBar.Focus();
        AddressBar.SelectAll();
    }
}
