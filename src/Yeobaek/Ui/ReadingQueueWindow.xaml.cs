using System.Windows;
using System.Windows.Input;
using Yeobaek.Data;

namespace Yeobaek.Ui;

/// <summary>나중에 읽기 목록 창. 앱에 하나만 띄우고, 글은 마지막으로 연 브라우저 창의 새 탭으로 연다.</summary>
public partial class ReadingQueueWindow : Window
{
    private static ReadingQueueWindow? _current;

    private readonly ReadingQueue _queue;
    private Action<string> _openUrl;

    private ReadingQueueWindow(ReadingQueue queue, Action<string> openUrl)
    {
        InitializeComponent();
        _queue = queue;
        _openUrl = openUrl;
        Closed += (_, _) => _current = null;
        Activated += (_, _) => Refresh();   // 다른 창에서 담은 글도 보이도록
    }

    public static void ShowSingle(AppServices services, Action<string> openUrl)
    {
        _current ??= new ReadingQueueWindow(services.Queue, openUrl);
        _current._openUrl = openUrl;
        _current.Show();
        if (_current.WindowState == WindowState.Minimized) _current.WindowState = WindowState.Normal;
        _current.Activate();
    }

    private void Refresh()
    {
        var entries = _queue.GetAll(unreadOnly: UnreadOnlyCheck.IsChecked == true);
        QueueList.ItemsSource = entries;
        EmptyText.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private List<QueueEntry> SelectedEntries() => QueueList.SelectedItems.OfType<QueueEntry>().ToList();

    private void OpenSelected()
    {
        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            StatusText.Text = StringTable.Get("Common.ChooseArticle");
            return;
        }
        foreach (var entry in selected)
        {
            _openUrl(entry.Url);
            _queue.SetRead(entry.Id, true);
        }
        Refresh();
    }

    private void UnreadOnlyCheck_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();

    private void QueueList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void QueueList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OpenSelected();
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            DeleteSelected();
        }
    }

    private void ToggleRead_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedEntries();
        foreach (var entry in selected) _queue.SetRead(entry.Id, !entry.IsRead);
        Refresh();
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        var selected = SelectedEntries();
        if (selected.Count == 0)
        {
            StatusText.Text = StringTable.Get("Queue.DeleteChoose");
            return;
        }
        foreach (var entry in selected) _queue.Delete(entry.Id);
        StatusText.Text = StringTable.Format("Queue.Removed", selected.Count);
        Refresh();
    }
}
