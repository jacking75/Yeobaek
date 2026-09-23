using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek.Browser;

/// <summary>
/// 우클릭 메뉴. 기본 메뉴에 항목을 끼워 넣는다(접근성·단축키 표시가 유지되고 구현이 짧다).
///
/// WebView2 는 환경마다 사용자 정의 항목을 1000개까지만 만들 수 있으므로 항목은 탭마다 한 번 만들어 재사용한다.
/// 우클릭 대상 정보(args, ContextMenuTarget)는 핸들러가 끝나면 무효가 되므로 필요한 값만 필드에 복사해 둔다.
/// </summary>
public sealed class ContextMenuService(TabManager tabs, BrowserTab tab)
{
    private MenuItems? _items;
    private string _linkUrl = "";
    private string? _linkText;
    private string _pageUrl = "";
    private bool _requestedForMainFrame;

    public void Attach() => tab.Core.ContextMenuRequested += OnContextMenuRequested;

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        var items = _items ??= CreateItems(tab.Core.Environment);
        var target = args.ContextMenuTarget;
        var menu = args.MenuItems;

        _pageUrl = target.PageUri;
        _requestedForMainFrame = target.IsRequestedForMainFrame;

        // Kind 에는 Link 가 없다. 이미지에 걸린 링크는 Kind == Image 이면서 HasLinkUri == true 다.
        if (target.HasLinkUri && SafeUrl.IsWebUrl(target.LinkUri))
        {
            _linkUrl = target.LinkUri;
            _linkText = target.HasLinkText ? target.LinkText : null;

            // 같은 일을 하는 기본 항목은 빼고 여백의 항목으로 대신한다
            RemoveByName(menu, "openLinkInNewWindow", "copyLinkLocation");
            var index = 0;
            foreach (var item in items.LinkItems) menu.Insert(index++, item);
        }

        if (!LocalPages.IsLocal(_pageUrl))
        {
            foreach (var item in items.PageItems) menu.Add(item);
        }

        RemoveByName(menu, "share", "webSelect", "webCapture");
    }

    private MenuItems CreateItems(CoreWebView2Environment environment)
    {
        var separator = () => environment.CreateContextMenuItem(null, null, CoreWebView2ContextMenuItemKind.Separator);

        return new MenuItems(
            LinkItems:
            [
                Command(environment, "새 탭에서 열기(&T)", () => tabs.OpenTab(_linkUrl, activate: false)),
                Command(environment, "새 탭에서 열고 이동(&N)", () => tabs.OpenTab(_linkUrl, activate: true)),
                Command(environment, "새 창에서 열기(&W)", () => MainWindow.Open(tabs.Services, _linkUrl)),
                Command(environment, "링크 주소 복사(&C)", () => CopyToClipboard(_linkUrl)),
                Command(environment, "나중에 읽기에 추가(&R)", AddLinkToQueue),
                separator(),
            ],
            PageItems:
            [
                separator(),
                Command(environment, "이 영역 광고 숨기기(&H)", StartPicker),
                Command(environment, "이 사이트 규칙 관리(&M)", ShowRuleEditor),
            ]);
    }

    /// <summary>
    /// 선택 시 동작은 디스패처 큐로 미뤄 메뉴가 닫힌 뒤에 실행한다.
    /// 메뉴 이벤트 처리 중에 창을 만들거나 클립보드를 여는 일을 피하기 위해서다.
    /// </summary>
    private CoreWebView2ContextMenuItem Command(CoreWebView2Environment environment, string label, Action onSelected)
    {
        var item = environment.CreateContextMenuItem(label, null, CoreWebView2ContextMenuItemKind.Command);
        item.CustomItemSelected += (_, _) => tab.View.Dispatcher.InvokeAsync(onSelected);
        return item;
    }

    private void AddLinkToQueue()
    {
        var added = tabs.Services.Queue.Add(_linkUrl, _linkText);
        tabs.Notify(Notice.Info(added ? "나중에 읽기에 추가했습니다." : "이미 나중에 읽기 목록에 있습니다."));
    }

    private void StartPicker()
    {
        var fromContextMenu = _requestedForMainFrame ? "true" : "false";
        _ = tab.Core.ExecuteScriptAsync($"window.__yeobaekPick && window.__yeobaekPick({fromContextMenu})");
        tab.View.Focus();   // Esc·방향키가 페이지로 가도록
    }

    private void ShowRuleEditor()
    {
        if (HostName.FromUrl(_pageUrl) is { } host) RuleEditorWindow.ShowFor(host, tabs.Services);
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch (COMException)
        {
            tabs.Notify(Notice.Error("다른 프로그램이 클립보드를 쓰고 있습니다. 잠시 후 다시 시도해 주세요."));
        }
    }

    private static void RemoveByName(IList<CoreWebView2ContextMenuItem> menu, params string[] names)
    {
        for (var i = menu.Count - 1; i >= 0; i--)
        {
            if (names.Contains(menu[i].Name, StringComparer.OrdinalIgnoreCase)) menu.RemoveAt(i);
        }
    }

    private sealed record MenuItems(
        IReadOnlyList<CoreWebView2ContextMenuItem> LinkItems,
        IReadOnlyList<CoreWebView2ContextMenuItem> PageItems);
}
