# Microsoft Store 무료 등록

- [x] 개인 개발자 무료 계정을 등록하고 본인 확인을 완료한다.
- [x] Store에서 앱 이름을 예약하고 실제 패키지 ID와 게시자 정보를 확인한다.
- [x] 자체 포함 .NET Windows x64 MSIX 패키지 생성 절차를 구현하고 검증한다.
- [x] Store 소개 문구, 개인정보 처리방침, 아이콘과 실제 화면 이미지를 준비한다.
- [x] 개인정보 처리방침을 공개 주소에 게시하고 접근을 확인한다.
- [x] 실제 Store ID로 최종 제출용 MSIX를 만든다.
- [ ] 최종 패키지의 설치·실행·제거와 Windows App Certification Kit를 확인한다.
- [x] 전체 시장의 기본 가격을 0(무료)로 저장하고 공개 배포로 설정한다.
- [x] 카테고리, 개인정보 처리방침과 지원 주소를 저장한다.
- [x] 제출용 MSIX를 업로드하고 Store 패키지 검증 결과를 확인한다.
- [x] 브라우저 기능을 반영한 IARC 연령 등급과 사용자 약관 동의를 저장한다.
- [x] Store 소개 페이지와 화면 이미지를 저장한다.
- [x] 제한된 기능 사용 이유와 심사 참고 사항을 저장한다.
- [x] Microsoft Store 심사를 제출하고 접수 상태를 확인한다.
- [ ] 심사 통과와 실제 무료 게시를 확인한다.

## 현재 상태

WinGet 작업은 `TODO.md`에 그대로 둔다. Microsoft Store 등록과는 별도로 관리한다.
사용자가 개인 개발자 등록과 본인 확인을 완료했다고 확인했다.
Partner Center에서 계정 접근을 확인하고 `여백` 이름을 예약했다.
Store ID는 `9P9727RR5675`다. 2026-10-08 23:22 KST에 심사 접수 후 `In certification` 상태와 전처리 진행을 확인했다. 심사 통과와 실제 게시는 아직 완료하지 않았다.
제출 ID는 `1152921505702075199`다. 모든 240개 시장에 기본 가격 `USD 0`을 저장했다. 공개 대상과 검색 가능한 배포를 선택했고, 제출 직전에도 저장된 무료 가격을 다시 확인했다.
Productivity 카테고리, 개인정보 처리방침과 GitHub 지원 주소를 저장했다.
Store에 실제 ID의 x64 MSIX `0.1.0.0`을 업로드하고 Windows Desktop 배포 설정을 저장했다.
패키지 수락 검증에는 `runFullTrust` 승인 필요 경고가 있다. WPF 데스크톱 앱 실행에 필요한 권한 사용 이유를 Submission options에서 설명하고 심사 승인을 받아야 한다.
IARC 설문에 웹 브라우저 기능을 반영했다. 생성된 등급에는 `Unrestricted Internet` 표시가 포함된다. 사용자가 IARC 약관 동의와 성인임을 확인한 뒤 저장했고, 개요의 Age ratings 상태가 Complete임을 확인했다.
한국어, 영어, 일본어와 중국어 간체 소개 및 실제 시작 화면 `home.png`를 저장했다. Store listings 상태가 Complete임을 확인했다.
Submission options에 `runFullTrust` 사용 이유와 심사 통과 후 즉시 게시를 저장했다. Additional Testing Info에 실행 안내를 저장하고 Successfully saved 응답을 확인했다.
`b12ea40` 커밋으로 패키지 생성 절차와 제출 자료를 `main`에 게시했다. GitHub API로 공개 저장소의 `PRIVACY.md` 접근을 확인했다.
사용할 개인정보 처리방침 주소는 <https://github.com/jacking75/Yeobaek/blob/main/PRIVACY.md>다.

## 제출 패키지와 심사 상태 확인

- 패키지: `.artifacts/microsoft-store/20261008-225833-296fddbc/jacking75.43679BBE8B1C1_0.1.0.0_x64.msix`
- 크기: 383,036,496바이트, Windows x64, 버전 `0.1.0.0`이다.
- SHA-256: `8B1193938FD71E344AE7E097FFDB39990769AB33F23E3E6072DF6C5521DD3AFF`
- 심사 접수 화면: 같은 산출물 폴더의 `store-certification.png`다. 로컬 산출물은 Git에 올리지 않는다.
- [Partner Center 개요](https://partner.microsoft.com/en-us/dashboard/products/9P9727RR5675/overview)에서 제출 상태와 심사 보고서를 확인한다.
- 심사 통과 후 자동 게시로 설정했다. 현재는 Submission 완료, Pre-processing 진행, Certification 및 Publishing 시작 전이다.
- 게시 후 확인할 [Store 주소](https://apps.microsoft.com/detail/9P9727RR5675)다. 현재 공개 설치 페이지가 확인됐다는 뜻은 아니다.

상태를 재확인할 때는 Partner Center에 로그인해 개요를 새로 연다. 실패하면 심사 보고서의 해당 요구 사항을 확인하고 수정·재제출한다. 성공하면 공개 Store 페이지에서 무료 취득 가능 여부와 Windows x64 설치·실행·제거를 확인한 뒤 마지막 게시 태스크를 체크한다. 심사 접수만으로 게시 완료를 기록하지 않는다.

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
    -IdentityName 'jacking75.43679BBE8B1C1' `
    -Publisher 'CN=E66A7C98-9426-41DB-9B55-AC993F05E1A3' `
    -PublisherDisplayName 'jacking75' `
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
- `packaging/store/listing.ja-JP.md`, `packaging/store/listing.zh-CN.md`: 일본어와 중국어 간체 소개다.
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
- 실제 Store ID의 최종 MSIX는 MakeAppx 검증과 Store 패키지 검증에 통과했다. 연령 등급과 무료 가격을 저장하고 심사를 제출했다. Store의 최종 심사 통과와 공개 게시·설치 검증은 남아 있다.

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
