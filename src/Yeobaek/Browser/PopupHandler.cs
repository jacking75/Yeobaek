using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek.Browser;

/// <summary>
/// 페이지가 새 창을 띄우려 할 때(window.open, target="_blank") 새 창 대신 새 탭으로 받는다.
/// WebView2 는 팝업 차단기를 끈 상태이므로, 사용자 조작 없이 뜨는 팝업(대부분 광고)은 여기서 막고
/// 알림의 StringTable.Get("Common.Open")로 직접 열 수 있게 한다.
/// </summary>
public sealed class PopupHandler(TabManager tabs, BrowserTab opener)
{
    public void Attach() => opener.Core.NewWindowRequested += OnNewWindowRequested;

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var url = e.Uri;
        e.Handled = true;   // 기본 새 창 동작 취소

        if (tabs.Services.Blocker.ShouldBlock(url)) return;
        if (!e.IsUserInitiated)
        {
            NotifyBlockedPopup(url);
            return;
        }

        var deferral = e.GetDeferral();   // 비동기 탭 생성 동안 이벤트 보류
        BrowserTab? tab = null;
        try
        {
            tab = await tabs.PrepareTabAsync(activate: true);
            e.NewWindow = tab.Core;        // 같은 환경, 아직 이동하지 않은 WebView 여야 한다. 이동은 WebView2 가 한다
            tabs.CompleteTab(tab);         // 네트워크 차단 등은 NewWindow 대입 뒤에 붙여야 한다
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, StringTable.Get("Log.PopupTab"));
            if (tab is not null) tabs.Close(tab);
            tabs.Notify(Notice.Error(StringTable.Get("Browser.NewTabFailed")));
        }
        finally
        {
            deferral.Complete();   // 빠뜨리면 여는 쪽 페이지가 멈춘 것처럼 보인다
        }
    }

    private void NotifyBlockedPopup(string url)
    {
        if (!SafeUrl.IsWebUrl(url)) return;
        var host = HostName.FromUrl(url) ?? url;
        tabs.Notify(Notice.Info(StringTable.Format("Browser.PopupBlocked", host), StringTable.Get("Common.Open"), () => tabs.OpenTab(url, activate: true)));
    }
}
