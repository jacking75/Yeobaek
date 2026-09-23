# spec.md

```markdown
# 여백 (Yeobaek) — 기술 명세

문서 버전 0.3 · 2026-09-22 · 대상 런타임 .NET 10 / WPF / WebView2

---

## 1. 아키텍처 개요

```
┌─────────────────────────────────────────────────┐
│ MainWindow (WPF)                                │
│  ┌───────────────────────────────────────────┐  │
│  │ TabStrip  │ AddressBar │ ReaderToggle     │  │
│  ├───────────────────────────────────────────┤  │
│  │ TabHost (ContentPresenter)                │  │
│  │   └─ BrowserTab.View : WebView2           │  │
│  └───────────────────────────────────────────┘  │
└───────┬─────────────────────────────────────────┘
        │
   ┌────┴──────────────────────────────────────┐
   │ TabManager                                │
   │  · 공유 CoreWebView2Environment 보유       │
   │  · 탭 생성/닫기/복원, NewWindowRequested   │
   └────┬──────────────────────────────────────┘
        │  탭마다 아래 서비스를 부착
   ┌────┴────────┬──────────────┬──────────────┐
   │RequestBlocker│CosmeticInject│ContextMenuSvc│
   │   (1층)      │   (2층)      │  (우클릭)    │
   └─────────────┴──────┬───────┴──────────────┘
                        │
                 ┌──────┴──────┐
                 │  RuleStore  │  SQLite
                 └─────────────┘
                 ┌─────────────┐
                 │ReaderService│  (3층) SmartReader
                 └─────────────┘
```

**설계 원칙 세 가지.** `CoreWebView2Environment`는 프로세스 전체에서
**단 하나**를 공유한다(탭 간 쿠키·세션 공유, 메모리 절약, `NewWindow` 대입
가능 조건). 차단은 성격이 다른 세 층으로 분리해 한 층이 뚫려도 다음 층이
받아낸다. 모든 상태(규칙·큐·설정)는 앱 데이터 폴더의 파일로만 보관해
동기화나 계정 개념을 두지 않는다.

---

## 2. 프로젝트 구성

```
yeobaek/
├─ src/Yeobaek/
│  ├─ Yeobaek.csproj
│  ├─ App.xaml(.cs)
│  ├─ MainWindow.xaml(.cs)
│  ├─ Browser/
│  │  ├─ TabManager.cs
│  │  ├─ BrowserTab.cs
│  │  ├─ RequestBlocker.cs
│  │  ├─ CosmeticInjector.cs
│  │  └─ ContextMenuService.cs
│  ├─ Reader/
│  │  └─ ReaderService.cs
│  ├─ Data/
│  │  ├─ RuleStore.cs
│  │  └─ AppPaths.cs
│  └─ Assets/
│     ├─ picker.js
│     └─ prelude.js
└─ spec.md / README.md
```

`Yeobaek.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Assets\yeobaek.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Web.WebView2" Version="*" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="*" />
    <PackageReference Include="SmartReader" Version="*" />
    <PackageReference Include="AngleSharp" Version="*" />
  </ItemGroup>
  <ItemGroup>
    <Resource Include="Assets\*.js" />
  </ItemGroup>
</Project>
```

WebView2 SDK의 NuGet 패키지 페이지는 프레임워크 호환성 표기가 최신 TFM을
아직 반영하지 못하는 경우가 있으나 `net10.0-windows` 빌드는 정상 동작한다.
경고가 나면 SDK를 최신 릴리스로 올린다.

---

## 3. 환경 초기화

```csharp
public static class AppPaths
{
    public static string Root { get; } = EnsureDir(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Yeobaek"));

    public static string RulesDb   => Path.Combine(Root, "rules.db");
    public static string BlockList => Path.Combine(Root, "blocklist.txt");
    public static string Profile   => EnsureDir(Path.Combine(Root, "WebView2"));

    private static string EnsureDir(string p) { Directory.CreateDirectory(p); return p; }
}
```

```csharp
public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
{
    var opts = new CoreWebView2EnvironmentOptions
    {
        AdditionalBrowserArguments = "--disable-features=Translate,msEdgeSidebar",
        Language = "ko-KR"
    };
    return await CoreWebView2Environment.CreateAsync(
        browserExecutableFolder: null,
        userDataFolder: AppPaths.Profile,
        options: opts);
}
```

탭별 `WebView2` 컨트롤은 반드시 이 환경을 넘겨 `EnsureCoreWebView2Async(env)`로
초기화한다. 환경이 다르면 `NewWindowRequested`에서 `e.NewWindow` 대입이
실패한다.

---

## 4. 1층 — 네트워크 차단

### 4.1 요구사항

`AddWebResourceRequestedFilter("*", All)`는 모든 요청이 관리 코드를
왕복시키므로 핸들러는 **마이크로초 단위**로 끝나야 한다. 정규식·LINQ
체인·동기 I/O·로깅을 핸들러 안에 두지 않는다. 판정 결과는 URL 기준으로
캐시한다.

### 4.2 구현

```csharp
public sealed class RequestBlocker
{
    private readonly HashSet<string> _hosts;
    private readonly string[] _pathHints;
    private readonly Dictionary<string, bool> _cache = new(StringComparer.Ordinal);

    public RequestBlocker(IEnumerable<string> hosts, IEnumerable<string> pathHints)
    {
        _hosts = new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase);
        _pathHints = pathHints.ToArray();
    }

    public static RequestBlocker LoadDefault()
    {
        // blocklist.txt: 한 줄에 하나. '#' 주석, '/'로 시작하면 경로 힌트
        var lines = File.Exists(AppPaths.BlockList)
            ? File.ReadAllLines(AppPaths.BlockList)
            : DefaultSeed;
        var body = lines.Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith('#'));
        var hints = body.Where(l => l.StartsWith('/')).ToList();
        var hosts = body.Where(l => !l.StartsWith('/')).ToList();
        return new RequestBlocker(hosts, hints);
    }

    private static readonly string[] DefaultSeed =
    {
        "pagead2.googlesyndication.com",
        "googleads.g.doubleclick.net",
        "securepubads.g.doubleclick.net",
        "tpc.googlesyndication.com",
        "ad.doubleclick.net",
        "display.ad.daum.net",
        "analytics.ad.daum.net",
        "static.criteo.net",
        "cas.criteo.com",
        "/adfit/",
        "/pagead/js/adsbygoogle.js",
    };

    public void Attach(CoreWebView2 core)
    {
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

        core.WebResourceRequested += (_, e) =>
        {
            if (!ShouldBlock(e.Request.Uri)) return;
            // 204 빈 응답이 요청 취소보다 사이트 스크립트를 덜 깨뜨린다
            e.Response = core.Environment.CreateWebResourceResponse(
                null, 204, "No Content", "Access-Control-Allow-Origin: *");
        };

        // 최상위 문서 이동은 WebResourceRequested로 잡히지 않는 경우가 있다
        core.NavigationStarting += (_, e) =>
        {
            if (ShouldBlock(e.Uri)) e.Cancel = true;
        };
    }

    private bool ShouldBlock(string url)
    {
        if (_cache.TryGetValue(url, out var hit)) return hit;

        var result = false;
        if (Uri.TryCreate(url, UriKind.Absolute, out var u))
        {
            if (_hosts.Contains(u.Host)) result = true;
            else
            {
                var path = u.AbsolutePath;
                foreach (var h in _pathHints)
                    if (path.Contains(h, StringComparison.OrdinalIgnoreCase))
                    { result = true; break; }
            }
        }

        if (_cache.Count > 20_000) _cache.Clear();   // 무한 증가 방지
        _cache[url] = result;
        return result;
    }
}
```

### 4.3 알려진 한계

`WebResourceRequested`는 런타임 버전에 따라 서비스 워커·일부 프리로드
컨텍스트에서 발생한 요청을 놓칠 수 있다. 이것이 2층·3층을 두는 이유다.

---

## 5. 2층 — 문서 생성 전 주입

### 5.1 핵심 이점

`AddScriptToExecuteOnDocumentCreatedAsync`로 등록한 스크립트는 문서 생성 직후,
**페이지의 첫 스크립트보다 먼저** 실행된다. 확장으로는 도달할 수 없는 위치이며,
광고 자리가 잠깐 보였다 사라지는 깜빡임도 발생하지 않는다.

### 5.2 스크립트 누적 주의

등록한 스크립트는 제거하지 않으면 **계속 쌓인다.** 반환되는 ID를 보관해
갱신 시 `RemoveScriptToExecuteOnDocumentCreated(id)`로 지운다. 여백은 규칙
전체를 한 번만 주입하고 도메인 판별을 주입된 JS 내부에서 `location.hostname`으로
처리하는 방식을 택해 재등록 빈도를 낮춘다.

### 5.3 구현

```csharp
public sealed class CosmeticInjector
{
    private readonly RuleStore _store;
    private string? _scriptId;

    public CosmeticInjector(RuleStore store) => _store = store;

    public async Task RefreshAsync(CoreWebView2 core)
    {
        if (_scriptId is not null)
        {
            core.RemoveScriptToExecuteOnDocumentCreated(_scriptId);
            _scriptId = null;
        }

        // { "*": [...], "tistory.com": [...], ... }
        var map = _store.GetAllGroupedByHost();
        var json = JsonSerializer.Serialize(map);

        var js = $$"""
        (() => {
          const MAP = {{json}};
          const host = location.hostname;
          const sels = [];
          for (const [h, list] of Object.entries(MAP)) {
            if (h === '*' || h === host || host.endsWith('.' + h)) sels.push(...list);
          }
          if (sels.length) {
            const s = document.createElement('style');
            s.textContent = sels.join(',\n') + '{display:none !important}';
            (document.head || document.documentElement).appendChild(s);
            // head가 나중에 교체되는 사이트 대비
            new MutationObserver(() => {
              if (!s.isConnected)
                (document.head || document.documentElement).appendChild(s);
            }).observe(document.documentElement, {childList:true, subtree:true});
          }

          // 자동 광고 SDK 선점 — push를 무동작으로 만들어 삽입을 원천 차단
          try {
            Object.defineProperty(window, 'adsbygoogle', {
              value: { loaded: true, push() {} },
              writable: false, configurable: false
            });
          } catch (e) {}

          // 전면(비네트) 광고가 잠근 스크롤 상시 해제
          const unlock = () => {
            const b = document.body;
            if (b && getComputedStyle(b).overflow === 'hidden')
              b.style.setProperty('overflow', 'auto', 'important');
          };
          addEventListener('DOMContentLoaded', () => {
            unlock();
            new MutationObserver(unlock).observe(document.documentElement,
              { attributes: true, subtree: true, attributeFilter: ['style','class'] });
          });
        })();
        """;

        _scriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(js);
    }
}
```

### 5.4 전역 규칙 시드

```
ins.adsbygoogle
.adsbygoogle
.google-auto-placed
iframe[id^="aswift_"]
iframe[id^="google_ads_iframe"]
.revenue_unit_wrap
.revenue_unit_item
.kakao_ad_area
```

---

## 6. 탭 관리

### 6.1 데이터 모델

```csharp
public sealed class BrowserTab : INotifyPropertyChanged
{
    public Microsoft.Web.WebView2.Wpf.WebView2 View { get; }
    public CoreWebView2 Core => View.CoreWebView2;
    public string Title { get; private set; } = "새 탭";
    public string? Url   { get; private set; }
    public bool IsReaderMode { get; set; }

    public BrowserTab(Microsoft.Web.WebView2.Wpf.WebView2 view) => View = view;
    // Core.DocumentTitleChanged / SourceChanged 를 Title·Url 에 연결
}
```

### 6.2 TabManager

```csharp
public sealed class TabManager
{
    private readonly CoreWebView2Environment _env;
    private readonly RuleStore _store;
    private readonly RequestBlocker _blocker;
    private readonly Stack<string> _recentlyClosed = new();

    public ObservableCollection<BrowserTab> Tabs { get; } = new();
    public BrowserTab? Active { get; private set; }

    public TabManager(CoreWebView2Environment env, RuleStore store, RequestBlocker blocker)
        => (_env, _store, _blocker) = (env, store, blocker);

    /// <summary>탭 하나를 만들고 모든 서비스를 부착한다. navigate 가 null 이면 이동하지 않는다.</summary>
    public async Task<BrowserTab> CreateTabAsync(string? navigate, bool activate = true)
    {
        var view = new Microsoft.Web.WebView2.Wpf.WebView2();
        await view.EnsureCoreWebView2Async(_env);      // 공유 환경 필수

        var tab  = new BrowserTab(view);
        var core = view.CoreWebView2;

        ConfigureSettings(core);
        _blocker.Attach(core);
        await new CosmeticInjector(_store).RefreshAsync(core);
        new ContextMenuService(this, _store).Attach(core);
        AttachNewWindowHandling(core);
        AttachModifierClickBridge(core);

        Tabs.Add(tab);
        if (activate) Activate(tab);
        if (navigate is not null) core.Navigate(navigate);
        return tab;
    }

    private static void ConfigureSettings(CoreWebView2 core)
    {
        var s = core.Settings;
        // 링크 우클릭 메뉴를 쓰려면 반드시 true 여야 한다.
        // false 로 두면 ContextMenuRequested 이벤트가 아예 발생하지 않는다.
        s.AreDefaultContextMenusEnabled = true;
        s.IsStatusBarEnabled            = false;
        s.AreDevToolsEnabled            = true;   // 선택자 확인용
        s.IsGeneralAutofillEnabled      = false;
        s.IsPasswordAutosaveEnabled     = false;
        s.IsSwipeNavigationEnabled      = false;
    }

    public void Activate(BrowserTab tab) => Active = tab;   // 실제로는 뷰 전환 + 알림

    public void Close(BrowserTab tab)
    {
        if (tab.Url is { } u) _recentlyClosed.Push(u);
        Tabs.Remove(tab);
        tab.View.Dispose();     // 명시적 해제. 안 하면 브라우저 프로세스가 남는다
        if (ReferenceEquals(Active, tab)) Active = Tabs.LastOrDefault();
    }

    public Task<BrowserTab>? ReopenClosed()
        => _recentlyClosed.Count > 0 ? CreateTabAsync(_recentlyClosed.Pop()) : null;
}
```

### 6.3 `NewWindowRequested` — 팝업을 탭으로 유도

페이지가 `window.open()`이나 `target="_blank"`로 창을 띄우려 할 때 새 창을
만들지 않고 새 탭으로 받는다. 팝업 광고가 별도 창으로 튀어나오는 경로를
막는 효과도 있다.

```csharp
private void AttachNewWindowHandling(CoreWebView2 core)
{
    core.NewWindowRequested += async (_, e) =>
    {
        e.Handled = true;                       // 기본 새 창 동작 취소
        var deferral = e.GetDeferral();         // 비동기 탭 생성 동안 이벤트 보류
        try
        {
            // navigate: null — WebView2 가 e.Uri 로 직접 이동시킨다
            var tab = await CreateTabAsync(navigate: null,
                                           activate: !e.IsUserInitiated == false);
            e.NewWindow = tab.Core;             // 같은 환경이어야 성공
        }
        catch
        {
            e.Handled = false;                  // 실패 시 기본 동작으로 폴백
        }
        finally
        {
            deferral.Complete();                // 반드시 호출
        }
    };
}
```

주의점 셋. `e.NewWindow`에 대입할 `CoreWebView2`는 **이미 초기화가 끝난**
것이어야 하므로 `await EnsureCoreWebView2Async`를 먼저 마쳐야 한다. 대입한
탭에 대해 `Navigate()`를 따로 호출하면 이중 이동이 되므로 하지 않는다.
`deferral.Complete()`를 빠뜨리면 렌더러가 멈춘 것처럼 보인다.

---

## 7. 링크 우클릭 컨텍스트 메뉴

### 7.1 API 배경

`CoreWebView2ContextMenuRequestedEventArgs.ContextMenuTarget`이 우클릭 대상
정보를 준다. 여기서 **`CoreWebView2ContextMenuTargetKind` 열거형에는 `Link`
항목이 없다.** 종류는 `Page`, `Image`, `SelectedText`, `Audio`, `Video`이며,
링크 여부는 별도 속성 `HasLinkUri` / `LinkUri`로 판단해야 한다. 이미지에 걸린
링크를 우클릭하면 `Kind == Image`이면서 `HasLinkUri == true`가 된다.

또한 `Settings.AreDefaultContextMenusEnabled`가 `false`면 `ContextMenuRequested`
이벤트가 **발생하지 않는다.** 6.2의 설정에서 `true`로 두는 이유다.

### 7.2 방식 선택

기본 메뉴에 항목을 끼워 넣는 방식(A)과 WPF로 메뉴를 직접 그리는 방식(B)이
있다. 여백은 **A를 기본**으로 한다. 접근성·단축키 표시·번역이 유지되고 구현이
짧다. 커스텀 아이콘이나 완전한 디자인 통제가 필요해지면 B로 전환한다.

### 7.3 구현 (방식 A)

```csharp
public sealed class ContextMenuService
{
    private readonly TabManager _tabs;
    private readonly RuleStore _store;

    public ContextMenuService(TabManager tabs, RuleStore store)
        => (_tabs, _store) = (tabs, store);

    public void Attach(CoreWebView2 core)
    {
        core.ContextMenuRequested += (_, args) =>
        {
            var target = args.ContextMenuTarget;
            var menu   = args.MenuItems;
            var env    = core.Environment;
            var ui     = SynchronizationContext.Current;   // 핸들러 진입 시 캡처

            // ── 링크를 우클릭한 경우 ──────────────────────────
            if (target.HasLinkUri && !string.IsNullOrEmpty(target.LinkUri))
            {
                var url  = target.LinkUri;
                var text = target.HasLinkText ? target.LinkText : url;
                var i    = 0;

                menu.Insert(i++, Cmd(env, "새 탭에서 열기(&T)", () =>
                    Post(ui, () => _ = _tabs.CreateTabAsync(url, activate: false))));

                menu.Insert(i++, Cmd(env, "새 탭에서 열고 이동(&N)", () =>
                    Post(ui, () => _ = _tabs.CreateTabAsync(url, activate: true))));

                menu.Insert(i++, Cmd(env, "새 창에서 열기(&W)", () =>
                    Post(ui, () => _ = OpenInNewWindowAsync(url))));

                menu.Insert(i++, Cmd(env, "링크 주소 복사(&C)", () =>
                    Post(ui, () => Clipboard.SetText(url))));

                menu.Insert(i++, Cmd(env, "나중에 읽기에 추가(&R)", () =>
                    Post(ui, () => _store.AddToQueue(url, text))));

                menu.Insert(i++, env.CreateContextMenuItem(
                    null, null, CoreWebView2ContextMenuItemKind.Separator));
            }

            // ── 광고 정리 항목은 항상 노출 ────────────────────
            menu.Add(env.CreateContextMenuItem(
                null, null, CoreWebView2ContextMenuItemKind.Separator));

            menu.Add(Cmd(env, "이 영역 광고 숨기기(&H)", () =>
                Post(ui, () => _ = core.ExecuteScriptAsync("window.__yeobaekPick()"))));

            menu.Add(Cmd(env, "이 사이트 규칙 관리(&M)", () =>
                Post(ui, () => RuleEditorWindow.ShowFor(new Uri(target.PageUri).Host))));

            // ── 불필요한 기본 항목 제거 ───────────────────────
            RemoveByName(menu, "share", "webSelect", "webCapture");
        };
    }

    private static CoreWebView2ContextMenuItem Cmd(
        CoreWebView2Environment env, string label, Action onSelected)
    {
        var item = env.CreateContextMenuItem(
            label, null, CoreWebView2ContextMenuItemKind.Command);
        item.CustomItemSelected += (_, _) => onSelected();
        return item;
    }

    // CustomItemSelected 는 UI 스레드 보장이 없다. Post 로 되돌린다.
    private static void Post(SynchronizationContext? ctx, Action action)
    {
        if (ctx is not null) ctx.Post(_ => action(), null);
        else Application.Current.Dispatcher.BeginInvoke(action);
    }

    private static void RemoveByName(
        IList<CoreWebView2ContextMenuItem> menu, params string[] names)
    {
        for (var i = menu.Count - 1; i >= 0; i--)
            if (names.Contains(menu[i].Name, StringComparer.OrdinalIgnoreCase))
                menu.RemoveAt(i);
    }

    private async Task OpenInNewWindowAsync(string url)
    {
        var win = new MainWindow(_tabs.Environment, _store);   // 환경 공유
        win.Show();
        await win.OpenAsync(url);
    }
}
```

**구현상 주의.** `CustomItemSelected` 핸들러는 UI 스레드에서 호출된다는 보장이
없으므로, `MessageBox`·`Clipboard`·창 생성 같은 작업은 `SynchronizationContext.Post`나
`Dispatcher`로 UI 스레드에 되돌려야 한다. 또 `args`와 `ContextMenuTarget`은
이벤트 핸들러 범위를 벗어나면 유효하지 않으므로, 필요한 값(`LinkUri`,
`LinkText`, `PageUri`)은 **핸들러 안에서 지역 변수로 복사**해 클로저에 담는다.
라벨의 `&`는 접근 키 표시이며 기본 메뉴에서는 그대로 쓴다(직접 WPF 메뉴를
그리는 방식 B에서는 `_`로 치환해야 밑줄로 표시된다).

### 7.4 방식 B 개요 (전체 커스텀 메뉴)

```csharp
core.ContextMenuRequested += (_, args) =>
{
    args.Handled = true;
    var deferral = args.GetDeferral();
    var cm = new ContextMenu();
    cm.Closed += (_, _) => deferral.Complete();   // 반드시 완료 처리
    Populate(args, args.MenuItems, cm);           // Label 의 '&' → '_' 치환
    cm.IsOpen = true;
};
```

기본 항목을 선택했을 때는 `args.SelectedCommandId = item.CommandId`로
WebView2에 되돌려 실행시킨다.

### 7.5 수정키·가운데 클릭 브리지

우클릭 메뉴만으로는 부족하므로, 문서 초기화 스크립트에서 링크 클릭을 가로채
호스트로 알린다.

```javascript
// Assets/prelude.js 말미
document.addEventListener('click', (e) => {
  const a = e.target.closest?.('a[href]');
  if (!a) return;
  if (e.ctrlKey)  { e.preventDefault(); post('open-background', a.href); }
  else if (e.shiftKey) { e.preventDefault(); post('open-window', a.href); }
}, true);

// 가운데 버튼: auxclick 이 표준. mousedown 의 기본 스크롤 동작도 막는다
document.addEventListener('auxclick', (e) => {
  if (e.button !== 1) return;
  const a = e.target.closest?.('a[href]');
  if (!a) return;
  e.preventDefault();
  post('open-background', a.href);
}, true);
document.addEventListener('mousedown', (e) => {
  if (e.button === 1 && e.target.closest?.('a[href]')) e.preventDefault();
}, true);

function post(type, url) {
  window.chrome.webview.postMessage({ type, url });
}
```

```csharp
core.WebMessageReceived += (_, e) =>
{
    var msg = JsonSerializer.Deserialize<HostMessage>(e.WebMessageAsJson);
    switch (msg?.Type)
    {
        case "open-background": _ = _tabs.CreateTabAsync(msg.Url, activate: false); break;
        case "open-window":     _ = OpenInNewWindowAsync(msg.Url!); break;
        case "pick":            OnSelectorPicked(new Uri(core.Source).Host, msg.Selector!); break;
    }
};

private record HostMessage(string Type, string? Url, string? Selector);
```

`e.WebMessageAsJson`으로 들어오는 값은 **페이지가 통제하는 입력**이다. URL은
`Uri.TryCreate`로 검증하고 `http`/`https` 스킴만 허용한다. 선택자는 길이
상한을 두고 저장 전에 `document.querySelector`로 유효성을 확인한다.

---

## 8. 요소 선택기

`picker.js`를 문서 초기화 스크립트에 포함해 `window.__yeobaekPick()`으로
진입한다. 동작은 마우스 이동 시 대상 요소에 반투명 테두리를 겹쳐 보이고,
클릭 시 선택자를 만들어 `postMessage('pick')`으로 호스트에 전달하며, `Esc`로
취소한다.

선택자 생성 규칙은 안정성 순으로 우선순위를 둔다. 유효한 `id`가 있으면
`#id`, 다음으로 `[data-*]` 속성, 다음으로 숫자 3자리 이상을 포함하지 않는
클래스 최대 2개, 마지막 수단으로 `:nth-of-type()`. 난독화된 해시 클래스명
(`css-1a2b3c` 형태)은 배제한다. 조상 경로는 5단계로 제한한다.

호스트 측 저장과 즉시 반영:

```csharp
private void OnSelectorPicked(string host, string selector)
{
    if (selector.Length is 0 or > 512) return;
    _store.AddRule(host, selector);

    var js = $"document.querySelectorAll({JsonSerializer.Serialize(selector)})" +
             ".forEach(el => el.style.setProperty('display','none','important'))";
    _ = _tabs.Active?.Core.ExecuteScriptAsync(js);
}
```

---

## 9. 데이터 스키마

```sql
-- 요소 숨김 규칙. host = '*' 는 전역
CREATE TABLE IF NOT EXISTS rules(
  id       INTEGER PRIMARY KEY,
  host     TEXT NOT NULL,
  selector TEXT NOT NULL,
  added_at TEXT NOT NULL,
  UNIQUE(host, selector)
);
CREATE INDEX IF NOT EXISTS ix_rules_host ON rules(host);

-- 차단 예외 사이트 (즐겨 읽어 후원하고 싶은 곳)
CREATE TABLE IF NOT EXISTS allowlist(
  host TEXT PRIMARY KEY,
  added_at TEXT NOT NULL
);

-- 나중에 읽기
CREATE TABLE IF NOT EXISTS queue(
  id     INTEGER PRIMARY KEY,
  url    TEXT NOT NULL UNIQUE,
  title  TEXT,
  added_at TEXT NOT NULL,
  read_at  TEXT
);

-- 추출 본문 보관 + 전문 검색
CREATE TABLE IF NOT EXISTS articles(
  url      TEXT PRIMARY KEY,
  title    TEXT,
  author   TEXT,
  html     TEXT,
  saved_at TEXT NOT NULL
);
CREATE VIRTUAL TABLE IF NOT EXISTS articles_fts
  USING fts5(title, body, url UNINDEXED, tokenize='unicode61');
```

서브도메인을 포함한 규칙 조회:

```sql
SELECT selector FROM rules
WHERE host = '*' OR host = $h OR $h LIKE '%.' || host;
```

---

## 10. 3층 — 리더 모드

```csharp
public sealed class ReaderService
{
    private readonly HttpClient _http;
    public ReaderService(HttpClient http) => _http = http;

    public async Task<string> RenderAsync(string url)
    {
        var a = await Reader.ParseArticleAsync(url, httpClient: _http);
        if (!a.IsReadable)
            return "<p>본문을 추출하지 못했습니다. 원본 보기로 전환하세요.</p>";

        return $$"""
        <!doctype html><html lang="ko"><head><meta charset="utf-8">
        <style>
          :root { color-scheme: light dark; }
          body {
            max-width: 42rem; margin: 3rem auto; padding: 0 1.25rem;
            font: 1.0625rem/1.85 "Pretendard", "Malgun Gothic", system-ui, sans-serif;
            word-break: keep-all;              /* 한글 줄바꿈 품질 */
          }
          h1 { font-size: 1.75rem; line-height: 1.35; }
          .meta { color: #888; font-size: .875rem; margin-bottom: 2.5rem; }
          img, video { max-width: 100%; height: auto; border-radius: .5rem; }
          pre { background:#1e1e1e; color:#e6e6e6; padding:1rem;
                border-radius:.5rem; overflow-x:auto; line-height:1.6; }
          blockquote { border-left:3px solid #ccc; margin-left:0;
                       padding-left:1rem; color:#666; }
        </style></head><body>
          <h1>{{a.Title}}</h1>
          <div class="meta">{{a.Author}} · 약 {{a.TimeToRead.Minutes}}분</div>
          {{a.Content}}
        </body></html>
        """;
    }
}
```

`NavigateToString`에는 약 2MB 문자열 상한이 있다. 이미지가 많은 긴 글은
`SetVirtualHostNameToFolderMapping`으로 로컬 폴더를 가상 호스트에 매핑해
파일로 서빙한다. 이 구조가 오프라인 보관과도 자연히 맞물린다.

리더 모드는 반드시 **토글**로 둔다. SPA·로그인 필요·이미지 위주 사이트에서는
추출이 실패하므로 1·2층만 적용된 원본 렌더링으로 되돌아갈 수 있어야 한다.

---

## 11. 성능·자원 지침

`WebResourceRequested` 핸들러의 목표는 요청당 10µs 미만이다. 탭마다 렌더러
프로세스가 생기므로 탭 수가 늘면 메모리가 선형 증가한다. `TabManager.Close`에서
`WebView2.Dispose()`를 반드시 호출하고, 15개 이상 열리면 비활성 탭을
`TrySuspendAsync()`로 절전 상태로 돌린다. 문서 초기화 스크립트는 하나로
합쳐 주입해 등록 수를 최소화한다.

---

## 12. 보안 지침

`WebMessageReceived`로 들어오는 모든 값은 신뢰하지 않는 입력으로 취급한다.
URL은 스킴 화이트리스트(`http`, `https`)로 검증하고, `file:`이나 `javascript:`는
거부한다. C#에서 JS로 문자열을 넘길 때는 반드시 `JsonSerializer.Serialize`로
인코딩해 스크립트 삽입을 막는다. `IsPasswordAutosaveEnabled`와
`IsGeneralAutofillEnabled`는 꺼둔다. `AddHostObjectToScript`는 사용하지 않는다
(페이지에 .NET 객체를 노출할 필요가 없다).