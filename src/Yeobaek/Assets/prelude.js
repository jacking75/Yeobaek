// ─────────────────────────────────────────────────────────────────────────────
// prelude.js — 문서가 만들어진 직후, 페이지의 첫 스크립트보다 먼저 모든 프레임에서 실행된다.
//
// DocumentScriptInjector 가 이 파일과 picker.js 를 하나의 즉시 실행 함수로 감싸고
// 맨 앞에 설정을 넣는다. 두 파일의 최상위 이름은 같은 스코프를 공유한다.
//   const CONFIG = { token: string, allow: string[], rules: { [host | '*']: string[] } }
//
// 이 시점에는 document.documentElement 도 아직 없을 수 있으므로 DOM 에 의존하는 일은
// 이벤트(DOMContentLoaded 등)로 미룬다.
// ─────────────────────────────────────────────────────────────────────────────

// 규칙을 갱신하는 짧은 순간에는 옛 스크립트와 새 스크립트가 함께 등록될 수 있다. 한 번만 실행한다.
if (window.__yeobaekLoaded) return;
Object.defineProperty(window, '__yeobaekLoaded', { value: true });

const isTopFrame = window === window.top;

// 페이지 스크립트가 나중에 JSON.stringify 나 postMessage 를 바꿔치기해도 영향이 없도록
// 페이지보다 먼저 실행되는 지금 원본을 잡아 둔다.
const stringify = JSON.stringify;
const webview = isTopFrame && window.chrome && window.chrome.webview;
const rawPost = webview ? webview.postMessage.bind(webview) : null;

const hideSheet = new CSSStyleSheet();
let lastContextTarget = null;

// ── 공통 도구 ────────────────────────────────────────────────────────────────

/** 호스트로 메시지를 보낸다. 토큰이 맞지 않는 메시지는 호스트가 버린다. */
function postToHost(type, url, selector) {
  if (!rawPost) return;
  rawPost('{"type":' + stringify(type) +
          ',"token":' + stringify(CONFIG.token) +
          ',"url":' + stringify(url ?? null) +
          ',"selector":' + stringify(selector ?? null) + '}');
}

function hostMatches(host, domain) {
  return host === domain || host.endsWith('.' + domain);
}

/** 광고 예외(후원) 사이트인지. iframe 이면 최상위 문서의 호스트로 판단한다. */
function isAllowlistedPage() {
  let host = location.hostname;
  const ancestors = location.ancestorOrigins;
  if (ancestors && ancestors.length) {
    try { host = new URL(ancestors[ancestors.length - 1]).hostname; } catch (e) { }
  }
  return CONFIG.allow.some((domain) => hostMatches(host, domain));
}

// ── 2층: 요소 숨김 ───────────────────────────────────────────────────────────
// <style> 요소는 붙일 곳(documentElement)이 아직 없을 수 있고 엄격한 CSP 가 막기도 한다.
// 생성형 스타일시트(adoptedStyleSheets)는 두 문제가 없고, head 가 교체되어도 사라지지 않는다.

/** 규칙을 선택자마다 따로 넣어, 잘못된 선택자 하나가 나머지 전체를 무효로 만들지 않게 한다. */
function addHideRule(selector) {
  try {
    hideSheet.insertRule(selector + '{display:none !important}', hideSheet.cssRules.length);
    return true;
  } catch (e) {
    return false;
  }
}

function adoptHideSheet() {
  const sheets = document.adoptedStyleSheets;
  if (!sheets.includes(hideSheet)) document.adoptedStyleSheets = [...sheets, hideSheet];
}

function applyHideRules() {
  for (const [host, selectors] of Object.entries(CONFIG.rules)) {
    if (host === '*' || hostMatches(location.hostname, host)) selectors.forEach(addHideRule);
  }
  adoptHideSheet();
  // 페이지가 adoptedStyleSheets 를 통째로 바꿔 끼우는 경우에 대비해 한 번씩 다시 붙인다
  document.addEventListener('DOMContentLoaded', adoptHideSheet);
  window.addEventListener('load', adoptHideSheet);
}

/**
 * 자동 광고 SDK 선점. (adsbygoogle = window.adsbygoogle || []).push({}) 가 아무 일도 하지 않게 한다.
 * 대입이 예외를 던지면 strict 모드 페이지 스크립트가 깨지므로 setter 는 조용히 무시한다.
 */
function preemptAdSense() {
  const stub = { loaded: true, push() { } };
  try {
    Object.defineProperty(window, 'adsbygoogle', { get: () => stub, set: () => { }, configurable: false });
  } catch (e) { }
}

/**
 * 전면(비네트) 광고가 잠근 본문 스크롤을 푼다.
 * 모든 DOM 변화가 아니라 html·body 의 style/class 변화만 지켜봐 비용을 줄인다.
 */
function keepBodyScrollable() {
  const unlock = () => {
    const body = document.body;
    if (body && getComputedStyle(body).overflowY === 'hidden')
      body.style.setProperty('overflow-y', 'auto', 'important');
  };
  document.addEventListener('DOMContentLoaded', () => {
    unlock();
    const observer = new MutationObserver(unlock);
    const options = { attributes: true, attributeFilter: ['style', 'class'] };
    observer.observe(document.documentElement, options);
    if (document.body) observer.observe(document.body, options);
  });
}

// ── 링크 수정키·가운데 클릭 → 호스트가 탭/창으로 연다 (최상위 프레임만) ─────────

/** 이벤트 경로에서 http(s) 링크를 찾는다. 그림자 DOM 안의 링크도 찾도록 composedPath 를 쓴다. */
function webLinkFrom(event) {
  for (const node of event.composedPath()) {
    if (node instanceof HTMLAnchorElement || node instanceof HTMLAreaElement) {
      return /^https?:/i.test(node.href) ? node.href : null;
    }
  }
  return null;
}

function installLinkBridge() {
  window.addEventListener('click', (e) => {
    if (e.button !== 0 || !(e.ctrlKey || e.shiftKey)) return;
    const url = webLinkFrom(e);
    if (!url) return;   // javascript: 링크 등은 페이지의 기본 동작에 맡긴다
    e.preventDefault();
    if (e.ctrlKey && e.shiftKey) postToHost('open-foreground', url);
    else if (e.ctrlKey) postToHost('open-background', url);
    else postToHost('open-window', url);
  }, true);

  // 가운데 버튼: auxclick 이 표준. 링크 위 mousedown 의 기본 동작(자동 스크롤)도 막는다
  window.addEventListener('auxclick', (e) => {
    if (e.button !== 1) return;
    const url = webLinkFrom(e);
    if (!url) return;
    e.preventDefault();
    postToHost('open-background', url);
  }, true);
  window.addEventListener('mousedown', (e) => {
    if (e.button === 1 && webLinkFrom(e)) e.preventDefault();
  }, true);

  // 우클릭 메뉴의 "이 영역 광고 숨기기"가 선택기를 이 요소에서 시작하도록 기억해 둔다
  window.addEventListener('contextmenu', (e) => {
    const target = e.composedPath()[0];
    lastContextTarget = target instanceof Element ? target : null;
  }, true);
}

// ── 실행 ─────────────────────────────────────────────────────────────────────

if (!isAllowlistedPage()) {
  applyHideRules();
  preemptAdSense();
  keepBodyScrollable();
}
if (rawPost) installLinkBridge();
