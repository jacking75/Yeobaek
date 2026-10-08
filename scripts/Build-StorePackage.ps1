[CmdletBinding(DefaultParameterSetName = 'Store')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Store')][ValidateNotNullOrEmpty()][string]$IdentityName,
    [Parameter(Mandatory, ParameterSetName = 'Store')][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory, ParameterSetName = 'Store')][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [Parameter(Mandatory, ParameterSetName = 'Development')][switch]$Development,
    [string]$DisplayName = '여백',
    [ValidatePattern('^\d+\.\d+\.\d+\.0$')][string]$Version = '0.1.0.0',
    [string]$WebViewVersion = '154.0.4258.62'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($Development) {
    $IdentityName = 'Yeobaek.Development'
    $Publisher = 'CN=Yeobaek Development'
    $PublisherDisplayName = 'Yeobaek Development'
}
if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$') { throw 'Store 패키지 ID 형식이 올바르지 않다.' }
if (!$Development -and ($IdentityName -match 'REPLACE|Development' -or $Publisher -match 'REPLACE|Development')) {
    throw '개발용 ID는 Store 제출에 사용할 수 없다.'
}
[void][System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new($Publisher)
[void][version]::Parse($Version)
$sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeAppx = Get-ChildItem -LiteralPath $sdkBin -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\makeappx.exe' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (!$makeAppx) { throw 'Windows SDK의 x64 MakeAppx.exe를 설치해야 한다.' }

$runtimeFolder = & (Join-Path $PSScriptRoot 'Get-StoreWebViewRuntime.ps1') -Version $WebViewVersion
# 빌드마다 새 폴더를 사용해 기존 패키지와 실패한 빌드의 자료를 보존한다.
$buildId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$output = Join-Path $repoRoot ".artifacts\microsoft-store\$buildId"
$staging = Join-Path $output 'package'
$app = Join-Path $staging 'App'
New-Item -ItemType Directory -Path $app -Force | Out-Null

Push-Location $repoRoot
try {
    & dotnet publish src\Yeobaek\Yeobaek.csproj --configuration Release --runtime win-x64 --self-contained true `
        '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' "-p:PublishDir=$app\" `
        "-p:OutputPath=$output\build\" '-p:PlatformTarget=x64'
    if ($LASTEXITCODE -ne 0) { throw 'Store 앱 게시 빌드가 실패했다.' }
}
finally { Pop-Location }
Copy-Item -LiteralPath $runtimeFolder -Destination (Join-Path $app 'WebView2Runtime') -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $app 'LICENSE.txt')
& (Join-Path $PSScriptRoot 'New-StoreLogos.ps1') -OutputDirectory (Join-Path $staging 'Assets')

$manifest = [xml](Get-Content -LiteralPath (Join-Path $repoRoot 'packaging\store\AppxManifest.xml') -Raw -Encoding UTF8)
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.Version = $Version
$manifest.Package.Properties.DisplayName = $DisplayName
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifest.Package.Applications.Application.VisualElements.DisplayName = $DisplayName
$manifest.Save((Join-Path $staging 'AppxManifest.xml'))
$package = Join-Path $output "$($IdentityName)_$($Version)_x64.msix"
$packLog = Join-Path $output 'makeappx.txt'
& $makeAppx pack /d $staging /p $package /o > $packLog 2>&1
if ($LASTEXITCODE -ne 0) {
    Get-Content -LiteralPath $packLog -Tail 20
    throw 'MSIX 패키징 또는 매니페스트 검증이 실패했다.'
}

$receipt = [ordered]@{
    Development = [bool]$Development
    IdentityName = $IdentityName
    Publisher = $Publisher
    PublisherDisplayName = $PublisherDisplayName
    DisplayName = $DisplayName
    Version = $Version
    Architecture = 'x64'
    WebViewVersion = $WebViewVersion
    Package = $package
    Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    CreatedAt = (Get-Date).ToString('o')
    Signed = $false
    Submitted = $false
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'package-info.json') -Encoding UTF8
Write-Host "MSIX 생성: $package"
if ($Development) { Write-Warning '개발용 ID 패키지다. Store 제출용으로 사용할 수 없다.' }
return $package
