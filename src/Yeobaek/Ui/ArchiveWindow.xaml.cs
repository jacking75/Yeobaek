using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Yeobaek.Archive;
using Yeobaek.Data;

namespace Yeobaek.Ui;

/// <summary>보관함 창. 보관한 글을 찾아 보관본(오프라인)이나 원문으로 연다. 앱에 하나만 띄운다.</summary>
public partial class ArchiveWindow : Window
{
    private static string EmptyArchiveMessage => StringTable.Get("Archive.Empty");

    private static ArchiveWindow? _current;

    private readonly AppServices _services;
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Action<string> _openUrl;

    private ArchiveWindow(AppServices services, Action<string> openUrl)
    {
        InitializeComponent();
        _services = services;
        _openUrl = openUrl;
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            Refresh();
        };
        Closed += (_, _) => _current = null;
        Activated += (_, _) => Refresh();   // 다른 창에서 보관한 글도 보이도록
    }

    /// <param name="openUrl">글을 열 브라우저 창의 새 탭 열기</param>
    public static void ShowSingle(AppServices services, Action<string> openUrl)
    {
        _current ??= new ArchiveWindow(services, openUrl);
        _current._openUrl = openUrl;
        _current.Show();
        if (_current.WindowState == WindowState.Minimized) _current.WindowState = WindowState.Normal;
        _current.Activate();
        _current.SearchBox.Focus();
    }

    private void Refresh()
    {
        var query = SearchBox.Text.Trim();
        var results = _services.Articles.Search(query);
        ResultList.ItemsSource = results;

        var (count, totalBytes) = _services.Articles.Summary();
        EmptyText.Text = count == 0 ? EmptyArchiveMessage : StringTable.Format("Archive.NoResults", query);
        EmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = query.Length == 0
            ? StringTable.Format("Archive.Summary", count, FormatSize(totalBytes))
            : StringTable.Format("Archive.Results", count, results.Count);
    }

    private static string FormatSize(long bytes)
        => bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0}MB" : $"{bytes / 1024}KB";

    private List<ArticleSearchResult> SelectedResults() => ResultList.SelectedItems.OfType<ArticleSearchResult>().ToList();

    private void OpenSelected(bool original)
    {
        var selected = SelectedResults();
        if (selected.Count == 0 && ResultList.Items.Count == 1) selected = [(ArticleSearchResult)ResultList.Items[0]];
        if (selected.Count == 0)
        {
            StatusText.Text = StringTable.Get("Common.ChooseArticle");
            return;
        }
        foreach (var result in selected)
            _openUrl(original ? result.Article.Url : ArchiveService.PageUrlFor(result.Article));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 입력할 때마다가 아니라 잠깐 멈췄을 때 찾는다
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _searchDelay.Stop();
            Refresh();
            if (ResultList.Items.Count > 0)
            {
                ResultList.SelectedIndex = 0;
                (ResultList.ItemContainerGenerator.ContainerFromIndex(0) as ListViewItem)?.Focus();
            }
        }
        else if (e.Key == Key.Down && ResultList.Items.Count > 0)
        {
            e.Handled = true;
            ResultList.SelectedIndex = 0;
            (ResultList.ItemContainerGenerator.ContainerFromIndex(0) as ListViewItem)?.Focus();
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected(original: false);

    private void OpenSource_Click(object sender, RoutedEventArgs e) => OpenSelected(original: true);

    private void ResultList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected(original: false);

    private void ResultList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OpenSelected(original: false);
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            DeleteSelected();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        var selected = SelectedResults();
        if (selected.Count == 0)
        {
            StatusText.Text = StringTable.Get("Archive.DeleteChoose");
            return;
        }

        var message = selected.Count == 1
            ? StringTable.Format("Archive.DeleteOneConfirm", selected[0].Title)
            : StringTable.Format("Archive.DeleteManyConfirm", selected.Count);
        if (MessageBox.Show(this, message, StringTable.Get("Archive.DeleteTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        foreach (var result in selected) _services.Archive.Delete(result.Article);
        Refresh();
        StatusText.Text = StringTable.Format("Archive.Deleted", selected.Count);
    }
}
