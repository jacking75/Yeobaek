# 명세(Spec.md)와 다르게 구현한 부분

Spec.md 0.3 을 바탕으로 구현하면서, 그대로 두면 동작하지 않거나 버그·보안 문제가 되는 부분은
고쳐서 구현했다. 각 항목은 **명세의 문제 → 구현 → 이유** 순서다.
WebView2 동작은 Microsoft.Web.WebView2 1.0.4191.47 의 API 문서로 확인했다.

## 빌드·프로젝트

- **패키지 `Version="*"`** → 버전 고정. 빌드할 때마다 다른 버전이 들어오면 재현이 안 된다.
- **AngleSharp 직접 참조** → 제거. SmartReader 가 끌어오고, 앱 코드는 쓰지 않는다.
- **`<Resource Include="Assets\*.js">`** → `EmbeddedResource`. WPF `Application` 없이도 읽을 수 있다.
- **`Assets\yeobaek.ico` 없음** → 아이콘을 만들어 넣었다.
- **출력 위치** → 저장소 루트 `bin\<구성>\`, 프레임워크 폴더 없음(요청 사항). 테스트는 `bin\<구성>\tests\`.

## 1층 — 네트워크 차단 (`Browser/RequestBlocker.cs`, `BlockRules.cs`)

- **2인자 `AddWebResourceRequestedFilter("*", All)`** → 현재 SDK 에서 폐기 예정이며 iframe 에서 기대대로
  동작하지 않는다고 문서에 적혀 있다. 3인자 오버로드로 탭마다 `Document` 를 걸고,
  `ServiceWorker|SharedWorker` 는 **한 탭에만** 건다(워커 요청은 필터를 건 모든 탭에 중복으로 올라온다).
  명세 §4.3 의 "서비스 워커 요청을 놓친다"는 한계도 이것으로 줄였다.
- **호스트 정확히 일치만 차단** → 하위 도메인까지 차단(`x.ad.example.com` 도 `ad.example.com` 규칙에 걸림).
- **URL 단위 캐시 + `Uri.TryCreate`** → 제거. 광고 URL 은 쿼리가 매번 달라 캐시 적중률이 낮고,
  문자열 조각(span)만 보는 판정이 캐시 조회보다 싸다. 할당 없이 끝난다.
- **allowlist 테이블은 있는데 쓰는 곳이 없음** → 예외 사이트는 1층(그 페이지의 요청)과 2층 모두에서 건너뛴다.
- **blocklist.txt 가 곧 기본 목록** → 파일이 이미 있으면 앱을 업데이트해도 새 기본 항목이 들어가지 않는다.
  기본 목록은 앱에 내장하고(`BlockRules.BuiltIn`), blocklist.txt 에는 더할 항목과 뺄 항목(`!호스트`)만 둔다.
  창이 다시 활성화될 때 파일이 바뀌었으면 다시 읽는다.
- 기본 목록에 카카오 애드핏 SDK, 쿠팡 파트너스 위젯, 네이버 광고 라이브러리 등을 더했다(티스토리 실제 페이지로 확인).
  Google 광고 차단 복구(`fundingchoicesmessages.google.com`)는 넣지 않는다. 막으면 페이지의 오류 보호 코드가
  "광고 차단 소프트웨어가 방해하고 있습니다" 경고를 띄우는 것을 확인했다.

## 2층 — 문서 생성 전 주입 (`Browser/DocumentScriptInjector.cs`, `Assets/prelude.js`)

- **`(document.head || document.documentElement).appendChild(style)`** → 문서 생성 직후에는
  `documentElement` 가 아직 없어 TypeError 로 스크립트 전체가 멈춘다. 엄격한 CSP 는 `<style>` 도 막는다.
  생성형 스타일시트(`document.adoptedStyleSheets`)로 바꿨다. head 가 교체돼도 사라지지 않는다.
- **선택자를 `,` 로 이어 규칙 하나로 만듦** → 잘못된 선택자 하나가 규칙 전체를 무효로 만든다.
  선택자마다 `insertRule` 로 따로 넣는다.
- **`adsbygoogle` 을 `writable:false` 로 고정** → strict 모드 페이지에서 대입이 예외를 던져 페이지 스크립트가 깨진다.
  getter + 아무것도 안 하는 setter 로 바꿨다.
- **스크롤 해제용 MutationObserver 가 문서 전체(subtree) 속성 변화를 감시하며 매번 `getComputedStyle`** →
  애니메이션이 많은 페이지에서 비싸다. html·body 의 style/class 변화만 본다.
- **탭마다 `new CosmeticInjector(...)` 후 버림** → 규칙이 바뀌어도 갱신할 방법이 없다. 하나의 주입기가 모든 탭의 등록을 관리하고,
  규칙·예외가 바뀌면 **새 스크립트를 먼저 등록한 뒤 옛 것을 지운다**(반대 순서면 그 사이 열린 문서가 보호 없이 뜬다).
  두 스크립트가 잠깐 겹쳐도 한 번만 실행되게 막았다.
- **규칙을 바꾸자마자 새로 고침** → 재등록이 비동기라 옛 스크립트로 문서가 뜬다(실제 테스트에서 재현됨).
  재등록이 끝난 뒤 새로 고친다(`TabManager.ReloadWhenRulesApplied`).
- §11 "문서 초기화 스크립트는 하나로 합쳐 주입" → prelude.js + picker.js + 규칙 설정을 한 스크립트로 등록한다.

## 탭 (`Browser/TabManager.cs`, `PopupHandler.cs`)

- **`new WebView2()` 를 시각 트리에 넣기 전에 `EnsureCoreWebView2Async`** → WPF WebView2 는 창 핸들이 생겨야
  초기화가 끝나므로 배경 탭에서는 영원히 끝나지 않는다. `ContentPresenter` 로 뷰를 갈아 끼우면 페이지도 다시 로드된다.
  모든 탭의 WebView2 를 한 `Grid` 에 올려 두고 보이기만 바꾼다. 숨긴 탭은 컨트롤러가 보이지 않는 상태가 되어
  §11 의 `TrySuspendAsync()` 가 동작한다(15개 이상일 때).
- **NewWindowRequested 에서 모든 서비스를 붙인 탭을 `e.NewWindow` 에 대입** → WebView2 규칙상
  `AddScriptToExecuteOnDocumentCreatedAsync` 는 대입 **전**에 끝나야 하고, `WebResourceRequested` 는 대입 **뒤**에 붙여야 한다.
  탭 생성을 1단계(설정·스크립트)와 2단계(차단·이벤트)로 나눴다.
- **`activate: !e.IsUserInitiated == false`** → 뜻이 흐리고, WebView2 는 팝업 차단기가 꺼져 있다(문서).
  사용자 조작 없는 팝업은 막고 알림의 "열기"로 직접 열 수 있게 했다. 광고 호스트로 가는 팝업도 막는다.
- **실패 시 `e.Handled = false` 로 폴백** → 여백의 차단이 붙지 않은 팝업 창이 뜬다. 대신 오류 알림을 띄운다.
- **`_tabs.Environment`, `new MainWindow(env, store)`** → 명세 안에서 정의되지 않은 멤버다. 공유 서비스를
  `AppServices` 하나로 묶어 창·탭이 함께 쓴다.
- 탭을 닫을 때 서비스 연결 해제·패널에서 제거까지 하고, 마지막 탭을 닫으면 창을 닫는다. 페이지의 `window.close()` 도 처리한다.

## 우클릭 메뉴 (`Browser/ContextMenuService.cs`)

- **우클릭할 때마다 사용자 정의 항목 생성** → 환경마다 1000개 한도가 있고 문서가 재사용을 권한다. 탭마다 한 번 만들어 재사용한다.
- **클로저 안에서 `target.PageUri` 접근** → 명세 스스로 "핸들러 밖에서는 무효"라고 한 값이다. 핸들러 안에서 필드로 복사한다.
- **"CustomItemSelected 는 UI 스레드 보장이 없다"** → WebView2 이벤트는 UI 스레드에서 온다. 다만 메뉴가 닫힌 뒤
  실행되도록 디스패처로 미루는 구조는 유지했다.
- 같은 일을 하는 기본 항목(`openLinkInNewWindow`, `copyLinkLocation`)은 뺐다. 클립보드 사용 중 예외를 처리한다.
- **`RuleEditorWindow` 가 명세에 없음** → 구현했다(사이트별 규칙 보기·추가·삭제, 예외 사이트 설정).

## 메시지 브리지 (`Browser/HostMessage.cs`, `HostMessageBridge.cs`)

- **`JsonSerializer.Deserialize<HostMessage>`** → 기본이 대소문자 구분이라 `type`/`url` 이 `Type`/`Url` 에 들어가지 않아
  아무 동작도 하지 않는다. 형식이 틀린 페이지 메시지에는 예외도 던진다. 필드를 직접 읽고 실패하면 버린다.
- **페이지 스크립트도 `chrome.webview.postMessage` 를 부를 수 있음** → 주입 스크립트만 아는 세션 토큰을 붙이고,
  페이지보다 먼저 잡아 둔 `JSON.stringify`·`postMessage` 로 문자열 메시지만 보낸다. 토큰이 없거나 객체 메시지면 버린다.
- **링크 찾기 `e.target.closest('a[href]')`** → 그림자 DOM 안 링크를 못 찾고, SVG 링크의 `href` 는 문자열이 아니며,
  `javascript:` 링크의 클릭까지 가로챈다. `composedPath()` 로 찾고 http(s) 링크만 가로챈다.
  `Ctrl+Shift+클릭` 은 바로 이동하는 새 탭으로 연다.
- 브리지는 최상위 프레임에만 설치한다(iframe 의 메시지는 프레임 이벤트로 가서 이 핸들러에 오지 않는다).

## 요소 선택기 (`Assets/picker.js`)

- **저장 후 `_tabs.Active` 에 숨김 적용** → 메시지를 보낸 탭이 아닐 수 있다. 페이지에서 바로 숨기고, 호스트는 저장만 한다.
  호스트 이름은 메시지 내용이 아니라 보낸 문서의 주소(`e.Source`)에서 얻는다.
- 페이지 전체를 투명한 가림막으로 덮고 `elementsFromPoint` 로 아래 요소를 찾는다. 클릭이 광고(iframe)로 새지 않는다.
  닫힌 그림자 DOM 에 그려 페이지 CSS 와 섞이지 않는다. `↑`/`↓` 로 범위를 넓히고 좁힌다.
- 저장할 때 호스트의 `www.` 를 떼어 www 유무와 상관없이 같은 사이트로 본다.
- 잘못 고른 경우를 위해 알림에 "되돌리기"를 둔다.

## 3층 — 리더 모드 (`Reader/`)

- **`Reader.ParseArticleAsync(url, httpClient: _http)`** → SmartReader 0.11 에 없는 오버로드다. 또 다시 내려받으면
  로그인 세션·스크립트로 그려진 본문을 잃는다. 탭의 현재 DOM(`outerHTML`)을 `new Reader(url, html)` 로 파싱한다.
- **제목·저자를 HTML 에 그대로 삽입** → 마크업 주입이 된다. 인코딩하고, 리더 페이지는 CSP 로 스크립트를 전혀 실행하지 않는다.
- **`TimeToRead.Minutes`** → 분 성분(0~59)이라 1시간 넘는 글에서 틀린다. `TotalMinutes` 를 쓴다.
- **`NavigateToString`(2MB 제한)** → 항상 파일로 쓰고 가상 호스트(`reader.yeobaek.example`, RFC 6761 예약 도메인)로 서빙한다.
  다른 사이트는 이 파일을 읽을 수 없다(`Deny`). 이미지 핫링크 차단을 피하려고 리퍼러를 보내지 않는다.
- **추출 실패 시 "본문을 추출하지 못했습니다" 페이지로 이동** → 원래 화면을 그대로 두고 알림으로 알린다.
- **지연 로딩 이미지** → 티스토리 등은 화면 밖 이미지의 `src` 에 1픽셀 자리표시를 두고 진짜 주소를 `data-src` 에 둔다.
  SmartReader 는 `src` 가 있으면 바꾸지 않아 빈 이미지가 된다(실제 페이지에서 확인). 추출 전에 `data-src` 류를 `src` 로 올린다.

## 오프라인 보관 (`Archive/`) — §10 의 "오프라인 보관과 맞물린다"를 구체화

- 보관은 사용자가 요청할 때만(`Ctrl+S`) 한다. 리더 모드로 본 글을 모두 저장하면 이미지 때문에 디스크가 계속 는다.
- 이미지는 원문을 볼 때와 같은 Referer·User-Agent 로 내려받아 핫링크 차단을 피하고, 광고 호스트 이미지는 받지 않는다.
  받지 못한 이미지는 원래 주소로 남긴다(인터넷이 있을 때만 보임). 이미지 하나 15MB, 전체 90초 상한.
- 파일은 임시 폴더에 다 쓴 뒤 한 번에 바꿔 넣어, 실패해도 이전 보관본이 망가지지 않는다.
- 보관본은 가상 호스트(`archive.yeobaek.example`, 다른 사이트 접근 거부)로 열고, 주소창에는 원문 주소를 보여 준다.

## 데이터 (`Data/`)

- **`ix_rules_host` 인덱스** → `UNIQUE(host, selector)` 가 이미 host 를 앞에 둔 인덱스를 만든다. 뺐다.
- 전역 기본 규칙은 버전별 묶음으로 두고(`RuleStore.DefaultGlobalRules`), DB 버전(`PRAGMA user_version`)보다
  새로운 묶음만 심는다. 업데이트하면 새 기본 규칙은 들어오고, 사용자가 지운 규칙은 되살아나지 않는다.
- **하위 도메인 조회 SQL `$h LIKE '%.' || host`** → `_` 가 LIKE 와일드카드라 호스트에 `_` 가 있으면 잘못 맞는다.
  규칙 수가 적으므로 C#(`HostName.Matches`)에서 거른다.
- **`articles(html)` + `articles_fts(tokenize='unicode61')`** → 보관본은 이미지와 함께 있어야 오프라인으로 열리므로
  HTML·이미지는 `archive\<폴더>\` 에 파일로 두고, DB 에는 목록(`articles`: url·제목·저자·사이트·폴더·크기·보관 시각)과
  색인만 둔다. 색인 토크나이저는 `unicode61` 대신 `trigram` 이다. `unicode61` 은 띄어쓰기로만 나눠
  "여백은" 속 "여백", "전문검색" 속 "검색"을 찾지 못한다. 검색은 낱말마다 `LIKE '%낱말%'` 로 하며
  (3글자 이상이면 trigram 색인이 처리하고, 2글자도 결과는 정확하다) 모든 낱말이 든 글을 최근 순으로 보여 준다.
- RSS 구독은 로드맵에서 뺐다(사용자 결정).

## 단축키·화면

- **`Ctrl+R`(리더 모드)** 는 WebView2 의 새로 고침과 겹친다. WebView2 는 가속키를 WPF 키 이벤트로 넘겨주므로
  창의 `PreviewKeyDown` 에서 처리하고 기본 동작을 막는다. 가속키 처리 중에는 WebView2 API 호출이 실패할 수 있어 디스패처로 미룬다.
  새로 고침은 `F5`.
- WebView2 는 WPF 요소 위에 그려지므로(에어스페이스) 알림 줄은 내용 영역 밖(창 아래)에 둔다.
- 명세에 없던 화면 요소: 새 탭 안내 화면(빈 상태), 로딩 막대, 차단 수·예외 사이트 표시, 나중에 읽기 창,
  동영상 전체 화면, 아이콘 버튼의 접근성 이름.
