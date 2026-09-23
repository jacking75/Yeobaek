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
        ShowStatus("바뀐 설정은 페이지를 새로 고치면 반영됩니다.");
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
            ShowStatus($"선택자를 확인해 주세요. 비어 있지 않아야 하고, 중괄호({{ }})는 쓸 수 없으며 {SelectorRules.MaxLength}자 이하여야 합니다.", isError: true);
            return;
        }

        var scope = GlobalCheck.IsChecked == true || _host.Length == 0 ? HostName.Global : _host;
        if (!_rules.AddRule(scope, selector))
        {
            ShowStatus("이미 있는 규칙입니다.");
            return;
        }
        SelectorBox.Clear();
        Refresh();
        ShowStatus("규칙을 추가했습니다. 페이지를 새로 고치면 반영됩니다. 올바른 CSS 선택자가 아니면 적용되지 않습니다.");
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
            ShowStatus("삭제할 규칙을 목록에서 먼저 고르세요.");
            return;
        }
        _rules.DeleteRules(selected.Select(rule => rule.Id));
        Refresh();
        ShowStatus($"규칙 {selected.Count}개를 삭제했습니다. 페이지를 새로 고치면 반영됩니다.");
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "ErrorBrush" : "MutedTextBrush");
    }
}
