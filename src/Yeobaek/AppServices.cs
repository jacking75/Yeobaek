using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Archive;
using Yeobaek.Browser;
using Yeobaek.Data;
using Yeobaek.Reader;

namespace Yeobaek;

/// <summary>
/// 모든 창과 탭이 함께 쓰는 서비스. CoreWebView2Environment 는 프로세스 전체에서 하나만 만든다
/// (탭 간 쿠키·세션 공유, 메모리 절약, NewWindowRequested 에서 NewWindow 대입이 가능하려면 같은 환경이어야 함).
/// </summary>
public sealed class AppServices
{
    private AppServices(
        CoreWebView2Environment environment, SettingsFile settings, RuleStore rules, ReadingQueue queue,
        ArticleStore articles, RequestBlocker blocker, DocumentScriptInjector scripts, ReaderService reader,
        ArchiveService archive, string messageToken)
    {
        Environment = environment;
        Settings = settings;
        Rules = rules;
        Queue = queue;
        Articles = articles;
        Blocker = blocker;
        Scripts = scripts;
        Reader = reader;
        Archive = archive;
        MessageToken = messageToken;
    }

    public CoreWebView2Environment Environment { get; }
    public SettingsFile Settings { get; }
    public RuleStore Rules { get; }
    public ReadingQueue Queue { get; }
    public ArticleStore Articles { get; }
    public RequestBlocker Blocker { get; }
    public DocumentScriptInjector Scripts { get; }
    public ReaderService Reader { get; }
    public ArchiveService Archive { get; }

    /// <summary>주입 스크립트만 아는 세션 토큰. 페이지 스크립트가 흉내 낸 호스트 메시지를 걸러낸다.</summary>
    public string MessageToken { get; }

    public static async Task<AppServices> CreateAsync()
    {
        var settings = new SettingsFile(AppPaths.Settings);

        var database = new Database(AppPaths.RulesDb);
        database.EnsureCreated(RuleStore.SeedGlobalRules);
        var rules = new RuleStore(database);

        var articles = new ArticleStore(database);
        var blocker = new RequestBlocker(rules, AppPaths.BlockList);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var environment = await CreateEnvironmentAsync();

        return new AppServices(
            environment, settings, rules, new ReadingQueue(database), articles,
            blocker,
            new DocumentScriptInjector(rules, token),
            new ReaderService(settings),
            new ArchiveService(articles, settings, blocker),
            token);
    }

    /// <summary>창이 다시 활성화될 때 사용자가 직접 고친 파일을 다시 읽는다. 알릴 내용이 없으면 null.</summary>
    public string? ReloadChangedFiles()
    {
        var messages = new List<string>();
        if (Blocker.ReloadIfChanged() is { } count) messages.Add($"차단 목록을 다시 읽었습니다(항목 {count}개).");
        if (Settings.ReloadIfChanged()) messages.Add(Settings.Error ?? "설정을 다시 읽었습니다.");
        return messages.Count == 0 ? null : string.Join(" ", messages);
    }

    private static Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--disable-features=Translate,msEdgeSidebar",
            Language = "ko-KR",
        };
        return CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: AppPaths.Profile,
            options: options);
    }
}
