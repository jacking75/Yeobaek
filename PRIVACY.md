# 여백 개인정보 처리방침

적용 대상은 여백(Yeobaek) Windows 앱이다.

## PC에 보관하는 정보

여백은 광고 차단 규칙, 예외 사이트, 설정, 나중에 읽기 목록과 보관한 글을 사용자의 PC에 저장한다.
보관한 글에는 원문 주소, 제목, 본문과 내려받은 이미지가 포함될 수 있다.
WebView2 프로필에는 방문 사이트의 쿠키, 로그인 상태, 캐시 등 브라우징 데이터가 저장될 수 있다.
오류가 발생하면 예외 메시지와 오류 위치를 로컬 `error.log`에 기록한다. 메시지에 주소나 파일 경로가 포함될 수 있다.
여백은 이 앱 데이터를 개발자 서버에 업로드하지 않는다. 계정 생성, 개발자 서버 동기화, 앱 자체의 사용 통계 전송 기능은 없다.

## 인터넷 통신

웹사이트를 열면 해당 사이트 및 페이지가 사용하는 서비스에 통신한다. 이 과정에서 IP 주소, 요청 주소, 브라우저 정보와 해당 사이트의 쿠키 등 정보가 전달될 수 있다.
주소 대신 검색어를 입력하면 설정한 검색 서비스로 검색어를 전송한다. 기본 검색 서비스는 Google이다.
글을 보관하면 이미지 서버에 이미지를 요청한다. 이미지 요청에는 원문 주소와 브라우저 정보가 포함될 수 있다.
방문 사이트와 검색 서비스의 정보 처리는 각 서비스의 개인정보 처리방침에 따른다.
광고 차단은 모든 광고나 추적 요청을 막는다는 보장이 아니다.

## Microsoft 구성 요소

여백은 Microsoft Edge WebView2를 사용한다. WebView2 및 Microsoft Store의 설치, 업데이트와 진단 등 Microsoft 서비스의 정보 처리는 Microsoft의 개인정보 처리방침에 따른다.

- [Microsoft 개인정보처리방침](https://privacy.microsoft.com/privacystatement)

## 삭제와 관리

보관함과 나중에 읽기 목록에서 항목을 삭제할 수 있다. 여백을 종료한 뒤 앱 데이터 폴더를 삭제하면 저장한 규칙, 설정, 글과 WebView2 프로필을 함께 삭제할 수 있다.
일반 실행 파일 버전의 기본 폴더는 `%LOCALAPPDATA%\Yeobaek`이다. Store 패키지는 Windows의 앱 데이터 경로 리디렉션을 적용받을 수 있다.
앱의 설정 파일·오류 기록 열기 기능으로 실제 파일 위치를 확인할 수 있다. 필요한 글과 설정은 삭제 전에 별도로 보관해야 한다.
Microsoft Store 버전 제거 시 Windows가 패키지별 데이터를 정리할 수 있으므로, 제거만으로 모든 배포 방식의 데이터를 삭제했다고 가정하지 않는다.

## 문의

[프로젝트 문의 페이지](https://github.com/jacking75/Yeobaek/issues)로 문의할 수 있다.
공개 문의에 비밀번호, 쿠키, 개인 문서나 민감한 오류 기록을 첨부하지 않아야 한다.

## Privacy policy in English

Yeobaek stores its settings, ad blocking rules, allowlisted sites, reading queue, saved articles and error logs on your PC. Saved articles may include source URLs, titles, text and downloaded images. The WebView2 profile may contain website cookies, sign-in state and cached browsing data. Error messages may contain URLs or file paths.

Yeobaek does not upload this app data to a developer server. It has no developer account service, cloud sync or application usage analytics upload feature.

Browsing sends requests to the websites and services used by each page. Requests may include your IP address, requested URL, browser information and website cookies. Searching sends your search terms to the configured search provider, which defaults to Google. Saving an article requests its images from their servers and may send the source page URL and browser information. Each website or search provider has its own privacy policy. Ad blocking does not guarantee that all tracking requests are blocked.

Yeobaek uses Microsoft Edge WebView2. Microsoft services, including WebView2 and Microsoft Store installation, updates and diagnostics, are governed by the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement).

You can delete articles and reading queue entries in the app. To delete all locally stored app data, close Yeobaek and delete its data folder. The portable app uses `%LOCALAPPDATA%\Yeobaek`; Windows may redirect paths for the Store package. The app's commands for opening settings and error logs help locate its actual files. Back up anything you want to keep before deleting data or uninstalling. Uninstalling one distribution does not necessarily remove data belonging to another distribution.

For questions, use [GitHub Issues](https://github.com/jacking75/Yeobaek/issues). Do not post passwords, cookies, private documents or sensitive logs in public issues.
