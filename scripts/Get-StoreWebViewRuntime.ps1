[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$Version = '154.0.4258.62'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $repoRoot ".artifacts\microsoft-store\cache\$Version"
$runtimeFolder = Join-Path $cache "Microsoft.WebView2.FixedVersionRuntime.$Version.x64"
$executable = Join-Path $runtimeFolder 'msedgewebview2.exe'
$cab = Join-Path $cache "Microsoft.WebView2.FixedVersionRuntime.$Version.x64.cab"
if (!(Test-Path -LiteralPath $executable)) {

    # 고정 버전 URL을 Microsoft 공식 메타데이터에서 확인한다. 오래된 버전을 조용히 바꾸지 않는다.
    $metadata = Invoke-RestMethod -Uri 'https://developer.microsoft.com/microsoft-edge/api/webview2' -TimeoutSec 30
    $release = @($metadata | Where-Object version -eq $Version)
    if ($release.Count -ne 1) { throw "공식 다운로드에서 WebView2 $Version 버전을 찾을 수 없다. 버전을 확인해야 한다." }
    $download = @($release[0].builds | Where-Object architecture -eq 'x64')
    if ($download.Count -ne 1) { throw 'x64 WebView2 다운로드를 찾을 수 없다.' }
    $downloadUri = [uri]$download[0].url
    if ($downloadUri.Scheme -ne 'https' -or $downloadUri.Host -ne 'msedge.sf.dl.delivery.mp.microsoft.com') {
        throw 'Microsoft 공식 다운로드 주소가 아니다.'
    }

    New-Item -ItemType Directory -Path $cache -Force | Out-Null
    if (!(Test-Path -LiteralPath $cab)) {
        $partial = "$cab.partial"
        & curl.exe --fail --location --retry 2 --max-time 300 --output $partial $downloadUri.AbsoluteUri
        if ($LASTEXITCODE -ne 0) { throw 'WebView2 다운로드가 실패했다.' }
        Move-Item -LiteralPath $partial -Destination $cab
    }
    & expand.exe $cab '-F:*' $cache | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'WebView2 압축 해제가 실패했다.' }
}
if (!(Test-Path -LiteralPath $executable)) { throw 'WebView2 실행 파일이 없다.' }
$signature = Get-AuthenticodeSignature -LiteralPath $executable
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'WebView2 실행 파일의 Microsoft 서명을 확인하지 못했다.'
}
Get-FileHash -LiteralPath $cab -Algorithm SHA256 |
    Select-Object Hash, Algorithm | ConvertTo-Json | Set-Content -LiteralPath "$cab.sha256.json" -Encoding UTF8
return $runtimeFolder
