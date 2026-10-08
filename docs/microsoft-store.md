# Microsoft Store 무료 등록

- [x] 개인 개발자 무료 계정을 등록하고 본인 확인을 완료한다.
- [ ] Store에서 앱 이름을 예약하고 실제 패키지 ID와 게시자 정보를 확인한다.
- [x] 자체 포함 .NET Windows x64 MSIX 패키지 생성 절차를 구현하고 검증한다.
- [x] Store 소개 문구, 개인정보 처리방침, 아이콘과 실제 화면 이미지를 준비한다.
- [ ] 개인정보 처리방침을 공개 주소에 게시하고 접근을 확인한다.
- [ ] 실제 Store ID로 최종 패키지를 만들고 설치·실행·제거와 Windows App Certification Kit를 확인한다.
- [ ] 가격을 Free로 설정하고 패키지·소개·연령 등급을 입력해 심사를 제출한다.
- [ ] 심사 통과와 실제 무료 게시를 확인한다.

## 현재 상태

WinGet 작업은 `TODO.md`에 그대로 둔다. Microsoft Store 등록과는 별도로 관리한다.
사용자가 개인 개발자 등록과 본인 확인을 완료했다고 확인했다.
Partner Center에서 계정 접근과 앱 이름 예약을 이어서 확인한다.
앱 이름 예약, 심사 제출과 게시를 완료한 것으로 표시하지 않는다.

## 무료 등록 경로

1. <https://storedeveloper.microsoft.com/>에서 무료 등록을 시작한다.
2. 개인 프로젝트라면 Individual developer를 선택한다. 회사 명의라면 Company를 선택한다.
3. Microsoft 계정으로 로그인하고 화면에 표시되는 본인 확인과 프로필 등록을 완료한다.
4. Partner Center의 Apps & Games에서 MSIX 앱 이름을 예약한다. `여백`을 우선 사용하고 이름이 이미 사용 중이면 다른 이름을 결정한다.
5. Product management → Product identity에서 `Package/Identity/Name`, `Package/Identity/Publisher`, `Package/Properties/PublisherDisplayName`을 정확하게 복사한다.

새 무료 등록 경로와 기존 유료 등록 경로는 다르므로 위 시작 주소를 사용한다.
MSIX 배포는 Store가 패키지를 서명한다. 유료 코드 서명 인증서는 구매하지 않는다.
로그인, 신분증·셀카 본인 확인과 계정 약관 확인은 계정 소유자가 직접 진행한다.

## 제출 설정

| 항목 | 값 |
| --- | --- |
| 배포 형식 | MSIX, Windows Desktop x64 |
| 기본 가격 | Free, 무료 |
| 체험판 / 인앱 결제 | 사용하지 않는다 |
| 공개 대상 | Public audience |
| 표시 | Store에서 검색하고 다운로드할 수 있도록 설정한다 |
| 게시 시점 | 심사 통과 후 가능한 즉시 |
| 카테고리 | Productivity를 우선 사용한다 |
| 지원 주소 | https://github.com/jacking75/Yeobaek/issues |

연령 등급은 웹 브라우저의 제한 없는 인터넷 접근을 사실대로 답변하고 Store가 생성한 결과를 사용한다.
실제 계정에서 가격과 제출 상태를 확인하기 전에는 무료 등록이 완료됐다고 기록하지 않는다.

## 패키지 만들기

Windows x64, .NET 10 SDK와 Windows SDK의 MakeAppx.exe가 필요하다. 빌드는 CI를 사용하지 않는다.

개발용 패키지 검증 명령은 다음과 같다. 이 결과는 Store에 제출하지 않는다.

```powershell
.\scripts\Build-StorePackage.ps1 -Development
```

이름 예약 후 실제 Store ID를 넣어 제출용 패키지를 만든다. 표시 이름은 예약한 이름과 일치시킨다.

```powershell
.\scripts\Build-StorePackage.ps1 `
    -IdentityName 'Partner Center의 Package/Identity/Name' `
    -Publisher 'Partner Center의 Package/Identity/Publisher' `
    -PublisherDisplayName 'Partner Center의 Package/Properties/PublisherDisplayName' `
    -DisplayName '여백'
```

산출물은 `.artifacts/microsoft-store/<빌드 ID>/`에 보관한다. `.msix`, 생성된 매니페스트, MakeAppx 출력과 `package-info.json`의 SHA-256을 확인한다.
`.NET` 런타임은 자체 포함 게시로 들어간다. Microsoft 공식 x64 WebView2 Fixed Version `154.0.4258.62`도 함께 들어간다.
설치된 WebView2가 같거나 더 최신이면 그 버전을 쓰고, 없거나 오래됐으면 포함한 버전을 쓴다.
Fixed Version 자체는 자동 업데이트하지 않으므로 이후 배포 때 최신 보안 수정 버전을 확인해 `-WebViewVersion`과 기본값을 함께 갱신한다.
Store는 MSIX를 서명한다. 로컬 개발용 MSIX는 서명하지 않으며, 신뢰된 개발 서명이나 개발자 모드 없이는 설치할 수 없다.

## 제출 자료

- `packaging/store/listing.ko-KR.md`: 한국어 소개와 검색어다.
- `packaging/store/listing.en-US.md`: 영어 소개와 검색어다.
- `PRIVACY.md`: 실제 데이터 저장과 인터넷 통신에 근거한 개인정보 처리방침이다. 공개 URL 접근을 확인한 뒤 등록한다.
- `packaging/store/certification-notes.md`: 심사 담당자에게 제공할 실행 안내와 `runFullTrust` 사용 이유다.
- `packaging/store/assets/`: 기존 앱 아이콘에서 렌더링한 패키지·Store 아이콘이다.
- `packaging/store/screenshots/`: Store 최소 크기를 만족하는 실제 앱 화면이다.

화면 이미지는 다음 명령으로 실행 중인 여백 창을 1600×1000 크기로 촬영한다. 촬영 후 창 크기를 복원한다. 촬영 결과를 열어 내용이 올바르게 표시되는지 확인한다.

```powershell
.\scripts\Capture-StoreScreenshot.ps1 -ProcessId <여백 프로세스 ID> `
    -OutputPath packaging\store\screenshots\home.png
```

## 로컬 검증 결과와 남은 검증

- `Build-StorePackage.ps1 -Development`: 종료 코드 0, MakeAppx 매니페스트 검증과 MSIX 생성에 성공했다.
- `dotnet test tests/Yeobaek.Tests/Yeobaek.Tests.csproj -c Release --blame-hang-timeout 120s --logger 'console;verbosity=minimal'`: 종료 코드 0, 기존 테스트 52개가 통과했다.
- 자체 포함 게시 폴더에서 앱 창과 시작 화면을 확인했다. 시스템 WebView2 사용과 포함한 WebView2 사용을 각각 실제 자식 프로세스의 실행 경로로 확인했다. 포함한 런타임 검증에는 프로세스에만 `WEBVIEW2_RELEASE_CHANNELS=1`을 설정해 시스템 Stable 채널을 제외했다.
- `Add-AppxPackage -Register <개발용 AppxManifest.xml>`: 종료 코드 1, `0x80073CFF`로 거부됐다. 이 컴퓨터의 개발자 모드·테스트용 로드 정책이 허용되지 않아 패키지 설치·실행·제거는 **AI 검증 못함**이다.
- Windows App Certification Kit는 관리자 권한과 패키지 설치가 필요한 별도 검증이다. 현재 **AI 검증 못함**이며, 일반 실행 파일 실행을 MSIX 설치 성공으로 간주하지 않는다.
- 실제 Store ID, 최종 패키지, 연령 등급, Free 가격 저장, 심사 제출과 게시 확인은 계정 등록 후 진행해야 한다.

최종 패키지는 설치가 허용되는 Windows x64 검증 환경에서 설치·실행·보관·제거를 확인한다. Windows 10 및 Windows 11 실기기 수락 검증은 아직 수행하지 않았다.
관리자 PowerShell에서 인증 키트를 실행하는 공식 명령은 다음과 같다. 자체 서명 개발 패키지는 먼저 검증용 환경에 신뢰를 설정해야 한다.

```powershell
$appcert = 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe'
& $appcert reset
& $appcert test -appxpackagepath <최종 패키지 경로> -reportoutputpath <보고서 XML 경로>
```

## 공식 참고 자료

- [무료 개발자 계정 등록](https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account)
- [첫 Windows 앱 배포와 MSIX 서명](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/publish-first-app)
- [MSIX 제출 절차](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission)
- [패키지 요구 사항](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [화면 이미지 요구 사항](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images)
- [WebView2 런타임 배포](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)
- [Windows App Certification Kit](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/windows-app-certification-kit)
