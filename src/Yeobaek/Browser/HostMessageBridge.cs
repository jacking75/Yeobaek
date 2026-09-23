using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek.Browser;

/// <summary>
/// 주입 스크립트가 보내는 메시지를 처리한다: 수정키·가운데 클릭 링크 열기, 요소 선택기 결과 저장.
/// 검증은 <see cref="HostMessage.Parse"/> 가 맡는다.
/// </summary>
public sealed class HostMessageBridge(TabManager tabs, BrowserTab tab)
{
    public void Attach() => tab.Core.WebMessageReceived += OnWebMessageReceived;

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try
        {
            raw = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            return;   // 주입 스크립트는 항상 문자열로 보낸다. 객체 메시지는 페이지가 보낸 것이다.
        }

        var message = HostMessage.Parse(raw, tabs.Services.MessageToken);
        switch (message?.Type)
        {
            case HostMessage.OpenBackground:
                tabs.OpenTab(message.Url, activate: false);
                break;
            case HostMessage.OpenForeground:
                tabs.OpenTab(message.Url, activate: true);
                break;
            case HostMessage.OpenWindow:
                MainWindow.Open(tabs.Services, message.Url);
                break;
            case HostMessage.Pick:
                // 호스트는 메시지 내용이 아니라 보낸 문서의 주소에서 얻는다
                SaveSelector(e.Source, message.Selector!);
                break;
        }
    }

    private void SaveSelector(string sourceUrl, string selector)
    {
        if (LocalPages.IsLocal(sourceUrl) || HostName.FromUrl(sourceUrl) is not { } host) return;

        var rules = tabs.Services.Rules;
        if (!rules.AddRule(host, selector))
        {
            tabs.Notify(Notice.Info("이미 저장된 규칙입니다."));
            return;
        }
        tabs.Notify(Notice.Info($"{host} 에서 이 영역을 계속 숨깁니다.", "되돌리기", () =>
        {
            rules.DeleteRule(host, selector);
            tabs.ReloadWhenRulesApplied(tab);
        }));
    }
}
