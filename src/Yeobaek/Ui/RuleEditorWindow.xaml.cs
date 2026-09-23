using System.Windows;
using System.Windows.Input;
using Yeobaek.Data;

namespace Yeobaek.Ui;

/// <summary>사이트별 숨김 규칙과 차단 예외 설정을 보고 고치는 창. 앱에 하나만 띄운다.</summary>
public partial class RuleEditorWindow : Window
{
    private static RuleEditorWindow? _current;

    private readonly RuleStore _rules;
    private string _host = "";

    private RuleEditorWindow(RuleStore rules)
    {
        InitializeComponent();
        _rules = rules;
        Closed += (_, _) => _current = null;
    }

    public static void ShowFor(string host, AppServices services)
    {
        _current ??= new RuleEditorWindow(services.Rules);
        _current.Load(host);
        _current.Show();
        if (_current.WindowState == WindowState.Minimized) _current.WindowState = WindowState.Normal;
        _current.Activate();
    }

    private void Load(string host)
    {
        _host = host.Trim() is { Length: > 0 } trimmed ? HostName.Normalize(trimmed) : "";
        HostBox.Text = _host;
        Refresh();
    }

    private void Refresh()
    {
        var hasHost = _host.Length > 0;
        AllowlistCheck.IsEnabled = hasHost;
        AllowlistCheck.IsChecked = hasHost && _rules.IsAllowlisted(_host);
        GlobalCheck.IsChecked = !hasHost;
        GlobalCheck.IsEnabled = hasHost;

        // 사이트를 고르지 않았으면 전역 규칙만 보여 준다
        var rules = hasHost ? _rules.GetRulesFor(_host) : _rules.GetRulesFor(HostName.Global).Where(r => r.Host == HostName.Global).ToList();
        RuleList.ItemsSource = rules;
        EmptyText.Visibility = rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadHost_Click(object sender, RoutedEventArgs e) => Load(HostBox.Text);

    private void HostBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        Load(HostBox.Text);
    }

    private void AllowlistCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_host.Length == 0) return;
        if (AllowlistCheck.IsChecked == true)
        {
            _rules.SetAllowlisted(_host, true);
        }
        else if (_rules.FindAllowlistEntry(_host) is { } entry)
        {
            _rules.SetAllowlisted(entry, false);   // 상위 도메인으로 예외가 걸려 있으면 그 항목을 푼다
        }
        Refresh();
        ShowStatus(StringTable.Get("Rules.SettingsChanged"));
    }

    private void AddRule_Click(object sender, RoutedEventArgs e) => AddRule();

    private void SelectorBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddRule();
    }

    private void AddRule()
    {
        var selector = SelectorBox.Text.Trim();
        if (!SelectorRules.IsAcceptable(selector))
        {
            ShowStatus(StringTable.Format("Rules.InvalidSelector", SelectorRules.MaxLength), isError: true);
            return;
        }

        var scope = GlobalCheck.IsChecked == true || _host.Length == 0 ? HostName.Global : _host;
        if (!_rules.AddRule(scope, selector))
        {
            ShowStatus(StringTable.Get("Rules.Exists"));
            return;
        }
        SelectorBox.Clear();
        Refresh();
        ShowStatus(StringTable.Get("Rules.Added"));
    }

    private void DeleteRules_Click(object sender, RoutedEventArgs e) => DeleteSelectedRules();

    private void RuleList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        e.Handled = true;
        DeleteSelectedRules();
    }

    private void DeleteSelectedRules()
    {
        var selected = RuleList.SelectedItems.OfType<RuleEntry>().ToList();
        if (selected.Count == 0)
        {
            ShowStatus(StringTable.Get("Rules.DeleteChoose"));
            return;
        }
        _rules.DeleteRules(selected.Select(rule => rule.Id));
        Refresh();
        ShowStatus(StringTable.Format("Rules.Deleted", selected.Count));
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "ErrorBrush" : "MutedTextBrush");
    }
}
