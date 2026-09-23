using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Yeobaek.Ui;

namespace Yeobaek;

// 알림 줄: 잠깐 보였다 사라진다. 마우스를 올려 두면 사라지지 않는다.
public partial class MainWindow
{
    private readonly DispatcherTimer _noticeTimer = new();
    private Notice? _notice;

    public void ShowNotice(Notice notice)
    {
        _notice = notice;
        NoticeText.Text = notice.Message;
        NoticeText.ToolTip = notice.Message;
        NoticeBar.Background = (Brush)FindResource(notice.IsError ? "ErrorBrush" : "NoticeBrush");
        NoticeActionButton.Content = notice.ActionLabel;
        NoticeActionButton.Visibility = notice.Action is null ? Visibility.Collapsed : Visibility.Visible;
        NoticeBar.Visibility = Visibility.Visible;

        _noticeTimer.Interval = TimeSpan.FromSeconds(notice.Action is null && !notice.IsError ? 5 : 10);
        _noticeTimer.Stop();
        if (!NoticeBar.IsMouseOver) _noticeTimer.Start();
    }

    private void HideNotice()
    {
        _noticeTimer.Stop();
        _notice = null;
        NoticeBar.Visibility = Visibility.Collapsed;
    }

    private void NoticeAction_Click(object sender, RoutedEventArgs e)
    {
        var action = _notice?.Action;
        HideNotice();
        action?.Invoke();
    }

    private void NoticeClose_Click(object sender, RoutedEventArgs e) => HideNotice();

    private void NoticeBar_MouseEnter(object sender, MouseEventArgs e) => _noticeTimer.Stop();

    private void NoticeBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_notice is not null) _noticeTimer.Start();
    }
}
