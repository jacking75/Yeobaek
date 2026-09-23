using System.IO;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;
using Yeobaek.Ui;

namespace Yeobaek.Browser;

/// <summary>
/// 1층 — 네트워크 차단. 모든 탭이 하나의 인스턴스를 공유한다.
/// 판정은 <see cref="BlockRules"/> 가 하고, 이 클래스는 탭(CoreWebView2)에 연결·해제와
/// 예외 사이트 처리를 맡는다.
/// </summary>
public sealed class RequestBlocker
{
    // 기본 목록은 앱에 들어 있으므로(BlockRules.BuiltIn) 이 파일에는 더하거나 뺄 항목만 적는다.
    private const string BlockListTemplate = """
        # 여백 네트워크 차단 목록 — 기본 목록은 여백에 들어 있고, 여기에는 더하거나 뺄 항목을 한 줄에 하나씩 적습니다.
        #   호스트      예) ads.example.com      (하위 도메인까지 차단)
        #   경로 힌트   예) /banner/             ('/' 로 시작, 주소 경로에 포함되면 차단)
        #   빼기        예) !static.criteo.net   (기본 목록의 항목을 앞에 '!' 를 붙여 적으면 차단하지 않음)
        #   '#' 으로 시작하는 줄은 주석입니다. 저장한 뒤 여백 창으로 돌아오면 바로 반영됩니다.

        """;

    private readonly RuleStore _rules;
    private readonly string _blockListPath;
    private readonly List<Attachment> _attachments = [];
    private BlockRules _blockRules;
    private DateTime _blockListWriteTimeUtc;

    // 서비스 워커·공유 워커 요청은 필터를 건 모든 탭에 중복으로 올라오므로 한 탭에만 건다.
    private Attachment? _workerFilterOwner;

    public RequestBlocker(RuleStore rules, string blockListPath)
    {
        _rules = rules;
        _blockListPath = blockListPath;
        _blockRules = LoadBlockList();
        rules.AllowlistChanged += RefreshPageAllowState;
    }

    public bool ShouldBlock(string url) => _blockRules.ShouldBlock(url);

    /// <param name="onRequestBlocked">이 탭의 문서가 보낸 요청을 막았을 때</param>
    /// <param name="onNavigationBlocked">최상위 문서 이동을 막았을 때 (막은 주소)</param>
    public void Attach(CoreWebView2 core, Action onRequestBlocked, Action<string> onNavigationBlocked)
    {
        var attachment = new Attachment(core, onRequestBlocked, onNavigationBlocked);
        _attachments.Add(attachment);

        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.Document);
        if (_workerFilterOwner is null) TakeWorkerFilter(attachment);

        core.WebResourceRequested += (_, e) => OnWebResourceRequested(attachment, e);
        // 최상위 문서 이동은 WebResourceRequested 로 잡히지 않는 경우가 있다
        core.NavigationStarting += (_, e) => OnNavigationStarting(attachment, e);
        core.SourceChanged += (_, _) => attachment.PageAllowed = IsAllowlistedPage(core.Source);
    }

    public void Detach(CoreWebView2 core)
    {
        var attachment = _attachments.Find(a => ReferenceEquals(a.Core, core));
        if (attachment is null) return;

        _attachments.Remove(attachment);
        if (!ReferenceEquals(_workerFilterOwner, attachment)) return;

        _workerFilterOwner = null;
        if (_attachments.Count > 0) TakeWorkerFilter(_attachments[0]);
    }

    /// <summary>blocklist.txt 가 바뀌었으면 다시 읽고 항목 수를 돌려준다. 그대로면 null.</summary>
    public int? ReloadIfChanged()
    {
        if (File.Exists(_blockListPath) && File.GetLastWriteTimeUtc(_blockListPath) == _blockListWriteTimeUtc) return null;
        _blockRules = LoadBlockList();
        return _blockRules.Count;
    }

    private void OnWebResourceRequested(Attachment attachment, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // 요청마다 불린다. 판정 외의 일(로깅·I/O)은 하지 않는다.
        if (!_blockRules.ShouldBlock(e.Request.Uri)) return;

        // 워커 요청은 어느 탭의 것인지 알 수 없으므로 예외 사이트와 상관없이 막고, 차단 수에도 넣지 않는다
        var fromDocument = e.RequestedSourceKind == CoreWebView2WebResourceRequestSourceKinds.Document;
        if (fromDocument && attachment.PageAllowed) return;

        // 204 빈 응답이 요청 취소보다 사이트 스크립트를 덜 깨뜨린다
        e.Response = attachment.Core.Environment.CreateWebResourceResponse(
            null, 204, "No Content", "Access-Control-Allow-Origin: *");
        if (fromDocument) attachment.OnRequestBlocked();
    }

    private void OnNavigationStarting(Attachment attachment, CoreWebView2NavigationStartingEventArgs e)
    {
        attachment.PageAllowed = IsAllowlistedPage(e.Uri);
        if (attachment.PageAllowed || !_blockRules.ShouldBlock(e.Uri)) return;

        e.Cancel = true;
        attachment.OnNavigationBlocked(e.Uri);
    }

    private void TakeWorkerFilter(Attachment attachment)
    {
        attachment.Core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.ServiceWorker | CoreWebView2WebResourceRequestSourceKinds.SharedWorker);
        _workerFilterOwner = attachment;
    }

    private bool IsAllowlistedPage(string? url)
        => HostName.FromUrl(url) is { } host && _rules.IsAllowlisted(host);

    private void RefreshPageAllowState()
    {
        foreach (var attachment in _attachments)
            attachment.PageAllowed = IsAllowlistedPage(attachment.Core.Source);
    }

    private BlockRules LoadBlockList()
    {
        try
        {
            if (!File.Exists(_blockListPath)) File.WriteAllText(_blockListPath, BlockListTemplate);

            _blockListWriteTimeUtc = File.GetLastWriteTimeUtc(_blockListPath);
            return BlockRules.WithBuiltIn(File.ReadAllLines(_blockListPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Error(ex, StringTable.Get("Log.ReadBlockList"));
            return BlockRules.WithBuiltIn([]);
        }
    }

    private sealed class Attachment(CoreWebView2 core, Action onRequestBlocked, Action<string> onNavigationBlocked)
    {
        public CoreWebView2 Core { get; } = core;
        public Action OnRequestBlocked { get; } = onRequestBlocked;
        public Action<string> OnNavigationBlocked { get; } = onNavigationBlocked;

        /// <summary>지금 보고 있는 문서가 예외 사이트인지. 이동이 시작될 때와 주소가 바뀔 때 갱신한다.</summary>
        public bool PageAllowed { get; set; }
    }
}
