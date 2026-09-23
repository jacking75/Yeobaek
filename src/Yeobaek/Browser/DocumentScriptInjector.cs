using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Yeobaek.Data;

namespace Yeobaek.Browser;

/// <summary>
/// 2층 — 문서 생성 전 주입. prelude.js(요소 숨김·광고 SDK 선점·링크 브리지)와 picker.js(요소 선택기)를
/// 규칙·예외 사이트 설정과 함께 하나의 스크립트로 묶어 모든 탭에 등록한다.
/// 규칙이 바뀌면 새 스크립트를 등록한 뒤 옛 스크립트를 지운다(등록한 스크립트는 지우지 않으면 쌓인다).
/// </summary>
public sealed class DocumentScriptInjector
{
    private static readonly string Prelude = ReadAsset("prelude.js");
    private static readonly string Picker = ReadAsset("picker.js");

    private readonly RuleStore _rules;
    private readonly string _token;
    private readonly List<Registration> _registrations = [];
    private string _script;
    private Task _pendingRefresh = Task.CompletedTask;

    public DocumentScriptInjector(RuleStore rules, string token)
    {
        _rules = rules;
        _token = token;
        _script = BuildScript();
        rules.RulesChanged += Refresh;
        rules.AllowlistChanged += Refresh;
    }

    /// <summary>탭에 스크립트를 등록한다. NewWindow 로 넘길 탭이면 대입 전에 끝나야 한다.</summary>
    public async Task AttachAsync(CoreWebView2 core)
    {
        var registration = new Registration(core);
        _registrations.Add(registration);
        await RegisterAsync(registration, _script);
    }

    public void Detach(CoreWebView2 core)
    {
        var registration = _registrations.Find(r => ReferenceEquals(r.Core, core));
        if (registration is null) return;
        registration.IsDetached = true;
        _registrations.Remove(registration);
    }

    /// <summary>
    /// 마지막 규칙 변경이 모든 탭에 등록될 때까지 기다린다.
    /// 규칙을 바꾸자마자 새로 고치면 옛 스크립트로 문서가 뜨므로, 새로 고침 전에 기다린다.
    /// </summary>
    public Task WhenUpdatedAsync() => _pendingRefresh;

    private void Refresh()
    {
        _script = BuildScript();
        _pendingRefresh = Task.WhenAll(_registrations.ToList().Select(registration => RefreshAsync(registration, _script)));
    }

    private static async Task RefreshAsync(Registration registration, string script)
    {
        try
        {
            await RegisterAsync(registration, script);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "문서 스크립트 갱신");
        }
    }

    /// <summary>
    /// 새 스크립트를 먼저 등록하고 옛 것을 지운다. 순서를 반대로 하면 그 사이에 열린 문서가 보호 없이 뜬다.
    /// 두 스크립트가 잠깐 함께 있어도 prelude.js 가 한 번만 실행되게 막는다.
    /// 갱신이 겹치면 가장 나중 요청만 남기고 나머지는 지운다.
    /// </summary>
    private static async Task RegisterAsync(Registration registration, string script)
    {
        var version = ++registration.Version;
        var id = await registration.Core.AddScriptToExecuteOnDocumentCreatedAsync(script);
        if (registration.IsDetached) return;

        if (version != registration.Version)
        {
            registration.Core.RemoveScriptToExecuteOnDocumentCreated(id);
            return;
        }
        if (registration.ScriptId is { } previous) registration.Core.RemoveScriptToExecuteOnDocumentCreated(previous);
        registration.ScriptId = id;
    }

    private string BuildScript()
    {
        // JsonSerializer 기본 인코더는 <, >, &, ', 비 ASCII 를 \uXXXX 로 바꾸므로 스크립트에 그대로 넣어도 안전하다
        var config = JsonSerializer.Serialize(new
        {
            token = _token,
            allow = _rules.GetAllowlist(),
            rules = _rules.GetAllGroupedByHost(),
        });
        return $"(() => {{\n'use strict';\nconst CONFIG = {config};\n{Prelude}\n{Picker}\n}})();";
    }

    private static string ReadAsset(string fileName)
    {
        using var stream = typeof(DocumentScriptInjector).Assembly.GetManifestResourceStream("Yeobaek.Assets." + fileName)
                           ?? throw new InvalidOperationException($"내장 리소스 {fileName} 을 찾을 수 없습니다.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class Registration(CoreWebView2 core)
    {
        public CoreWebView2 Core { get; } = core;
        public string? ScriptId { get; set; }
        public int Version { get; set; }
        public bool IsDetached { get; set; }
    }
}
