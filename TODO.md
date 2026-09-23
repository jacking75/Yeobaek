# WinGet 등록 TODO

- [ ] [등록 PR #439827](https://github.com/microsoft/winget-pkgs/pull/439827)의 관리자 검토를 확인하고, 요청된 변경 사항이 있으면 모두 반영한다.
- [ ] 변경 요청이 생기면 해당 버전의 매니페스트만 수정하고 `winget validate --manifest manifests/j/jacking75/Yeobaek/0.1.0`으로 검증한 뒤 같은 PR에 반영한다. 요청이 없으면 병합 시 완료로 표시한다.
- [ ] PR이 병합됐는지 확인한다.
- [ ] 병합 후 공개 목록에서 `winget show --id jacking75.Yeobaek -e --source winget`으로 패키지 정보와 버전 `0.1.0`을 확인한다.
- [ ] 검증용 Windows x64 환경에서 `winget install --id jacking75.Yeobaek -e`로 설치하고 여백 실행, .NET 10 Desktop Runtime·WebView2 의존성, `winget uninstall --id jacking75.Yeobaek -e`를 확인한다.
- [ ] 설치가 확인되면 `README.md`와 `README_kr.md`의 설치 안내에 WinGet 명령을 추가한다.

## 현재 상태

- `v0.1.0` 실행 파일은 [GitHub 릴리스](https://github.com/jacking75/Yeobaek/releases/tag/v0.1.0)에 게시돼 있다.
- 2026-09-24 확인 시 등록 PR은 열려 있으며 관리자 검토를 기다리고 있다. 개인 기여자 CLA 동의는 처리됐고 `Needs-CLA` 표시가 제거됐다.
- 로컬 매니페스트 문법 검증과 릴리스 파일의 SHA-256 대조를 마쳤다. 이 컴퓨터에서는 관리자 권한이 없어 로컬 매니페스트 설치 시험을 진행하지 못했다.
- 현재 공개 WinGet 목록에서 `jacking75.Yeobaek`은 검색되지 않는다. PR 병합과 게시가 끝난 뒤 다시 확인해야 한다.

## 검토가 지연되거나 수정 요청이 오면

- 댓글과 요청 사항을 먼저 확인한다. 검토가 오래 멈추면 [공식 검토 안내](https://github.com/microsoft/winget-pkgs/blob/master/doc/Moderation.md#before-you-request-assistance)에 따라 PR에서 정중하게 상태를 문의한다.
- 설치 URL이나 실행 파일이 바뀌면 SHA-256을 다시 계산하고 매니페스트와 릴리스 파일이 일치하는지 검증한다. 이미 게시한 `v0.1.0` 파일은 다른 내용으로 교체하지 않는다.

## 다음 버전부터 반복할 일

1. 버전이 고정된 새 릴리스에 Windows 실행 파일을 게시한다.
2. 새 파일의 SHA-256으로 WinGet 매니페스트의 새 버전 폴더를 만든다.
3. 로컬 검증 후 버전 하나만 담은 PR을 제출한다.
4. 병합·게시·설치 여부를 확인한다.
