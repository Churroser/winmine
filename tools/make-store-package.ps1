# Builds the Microsoft Store variant of the package (identity from Partner Center),
# producing an unsigned .msix plus the .msixupload archive Partner Center expects.
#
# Two size profiles:
#   default              .NET self-contained, Windows App SDK as a framework dependency
#                        (~45 MB) -- the Store installs Microsoft.WindowsAppRuntime.2 for us
#   -SelfContainedRuntime  everything bundled (~66 MB) -- for machines where the framework
#                        package cannot be deployed, e.g. a quick local sideload test
#
# Both profiles strip the Windows App SDK payload that this app never loads (see
# tools\package-trim.ps1); -NoTrim keeps it.
#
# ASCII only on purpose: Windows PowerShell 5.1 parses BOM-less scripts as ANSI,
# so the Chinese PublisherDisplayName lives in packaging\AppxManifest.Store.xml only.
#
#   powershell -File tools\make-store-package.ps1                   # build + pack + verify
#   powershell -File tools\make-store-package.ps1 -SkipBuild        # reuse artifacts\store-layout
#   powershell -File tools\make-store-package.ps1 -SelfContainedRuntime
#   powershell -File tools\make-store-package.ps1 -TestSign         # sign with a throwaway cert
#                                                                   # whose subject equals the
#                                                                   # Store Publisher, for a local
#                                                                   # Add-AppxPackage smoke test
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.4.0",
    [string]$OutputDir = "artifacts\store",
    [switch]$SkipBuild,
    [switch]$SelfContainedRuntime,
    [switch]$NoTrim,
    [switch]$TestSign,
    [string]$CertPassword = "fluent-sweeper-store"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
. (Join-Path $PSScriptRoot "package-trim.ps1")

$kit = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64"
$makeappx = Join-Path $kit "makeappx.exe"
$signtool = Join-Path $kit "signtool.exe"
foreach ($tool in @($makeappx, $signtool)) {
    if (-not (Test-Path $tool)) { throw "missing SDK tool: $tool" }
}

$layout = Join-Path $root "artifacts\store-layout"
$out = Join-Path $root $OutputDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

$storeManifestPath = Join-Path $root "packaging\AppxManifest.Store.xml"
$storeManifest = [IO.File]::ReadAllText($storeManifestPath, [Text.Encoding]::UTF8)
$identityName = [regex]::Match($storeManifest, 'Name="([^"]+)"').Groups[1].Value
$publisher = [regex]::Match($storeManifest, 'Publisher="([^"]+)"').Groups[1].Value
$publisherDisplay = [regex]::Match($storeManifest, '<PublisherDisplayName>([^<]+)</PublisherDisplayName>').Groups[1].Value
if (-not $identityName -or -not $publisher -or -not $publisherDisplay) {
    throw "could not read the Store identity out of packaging\AppxManifest.Store.xml"
}

if (-not $SkipBuild) {
    if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
    $wasdk = if ($SelfContainedRuntime) { "true" } else { "false" }
    Write-Host "building layout (Windows App SDK self-contained: $wasdk) ..."
    & dotnet build "src\WinMine\WinMine.csproj" -c $Configuration `
        -p:SelfContained=true -p:WindowsAppSDKSelfContained=$wasdk -p:WindowsPackageType=MSIX `
        -p:Version=$Version -o $layout
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
}

$required = @("WinMine.exe", "WinMine.pri", "Microsoft.WindowsAppRuntime.Bootstrap.dll")
if ($SelfContainedRuntime) { $required += "Microsoft.ui.xaml.dll" }
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $layout $name))) { throw "layout is incomplete, missing $name" }
}

if (-not $NoTrim) { Remove-OptionalPayload -Layout $layout }

Copy-Item $storeManifestPath (Join-Path $layout "AppxManifest.xml") -Force
$assetsDir = Join-Path $layout "Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
Copy-Item "packaging\Assets\*.png" $assetsDir -Force
Copy-Item "src\WinMine\Assets\winmine.ico" $assetsDir -Force
Copy-Item "src\WinMine\Assets\winmine.png" $assetsDir -Force

$manifest = [IO.File]::ReadAllText((Join-Path $layout "AppxManifest.xml"), [Text.Encoding]::UTF8)
if ($manifest -notmatch ('Version="' + [regex]::Escape($Version) + '\.0"')) {
    Write-Warning "AppxManifest.Store.xml Identity Version does not read $Version.0"
}
# The framework dependency must be declared exactly when the SDK is not bundled, and dropped
# when it is -- a self-contained package that still declares the dependency installs nothing
# but the app and then loads WinUI from the framework anyway.
$hasDependency = $manifest -match 'Microsoft\.WindowsAppRuntime\.2'
if ($SelfContainedRuntime -and $hasDependency) {
    Write-Warning "self-contained build still declares Microsoft.WindowsAppRuntime.2; remove it from packaging\AppxManifest.Store.xml to avoid a version mismatch."
}
if (-not $SelfContainedRuntime -and -not $hasDependency) {
    throw "the framework-dependent build needs a PackageDependency on Microsoft.WindowsAppRuntime.2"
}

$msix = Join-Path $out "FluentSweeper-$Version-x64.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
Write-Host "packing $msix ..."
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

# Partner Center re-signs Store packages, so the upload is unsigned. -TestSign produces a
# throwaway certificate whose subject equals the Store Publisher, which is what AppX checks
# when you install the package locally.
$cer = Join-Path $out "FluentSweeper-store-testsign.cer"
if ($TestSign) {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if (-not $cert) {
        Write-Host "creating a test-signing certificate for $publisher ..."
        $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $publisher `
            -FriendlyName "Fluent Sweeper Store test signing" `
            -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
            -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    }
    $pfx = Join-Path $out "FluentSweeper-store-testsign.pfx"
    $secure = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null
    Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null
    Write-Host "test-signing ..."
    & $signtool sign /fd SHA256 /f $pfx /p $CertPassword /tr http://timestamp.digicert.com /td SHA256 $msix
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed ($LASTEXITCODE)" }
    Remove-Item $pfx -Force
}

# .msixupload is a zip that carries the .msix payload; Partner Center's uploader wants the
# extension, and also accepts a bare .msix. Ship both.
$stage = Join-Path $root "artifacts\store-upload"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item $msix $stage -Force
$msixupload = Join-Path $out "FluentSweeper-$Version-x64.msixupload"
if (Test-Path $msixupload) { Remove-Item $msixupload -Force }
# Compress-Archive in Windows PowerShell 5.1 only writes .zip, so compress to a .zip name
# and rename it to .msixupload afterwards.
$zipStage = Join-Path $out "FluentSweeper-$Version-x64.msixupload.zip"
if (Test-Path $zipStage) { Remove-Item $zipStage -Force }
Compress-Archive -Path (Join-Path $stage "*.msix") -DestinationPath $zipStage -CompressionLevel Optimal
Move-Item $zipStage $msixupload -Force
Remove-Item $stage -Recurse -Force

# Read the package back: identity must match Partner Center byte for byte, or the upload is
# rejected with a generic identity error.
$check = Join-Path $root "artifacts\store-check"
if (Test-Path $check) { Remove-Item $check -Recurse -Force }
& $makeappx unpack /p $msix /d $check /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed ($LASTEXITCODE)" }

$packed = [IO.File]::ReadAllText((Join-Path $check "AppxManifest.xml"), [Text.Encoding]::UTF8)
$packedName = [regex]::Match($packed, 'Name="([^"]+)"').Groups[1].Value
$packedPublisher = [regex]::Match($packed, 'Publisher="([^"]+)"').Groups[1].Value
$packedDisplay = [regex]::Match($packed, '<PublisherDisplayName>([^<]+)</PublisherDisplayName>').Groups[1].Value
$packedVersion = [regex]::Match($packed, 'Version="([0-9.]+)"').Groups[1].Value
$arch = [regex]::Match($packed, 'ProcessorArchitecture="([^"]+)"').Groups[1].Value
$fileCount = (Get-ChildItem $check -Recurse -File | Measure-Object).Count
$dependency = [regex]::Match($packed, '<PackageDependency[^>]*?Name="([^"]+)"', 'Singleline').Groups[1].Value

$okName = $packedName -ceq $identityName
$okPublisher = $packedPublisher -ceq $publisher
$okDisplay = $packedDisplay -ceq $publisherDisplay
Write-Host ""
Write-Host "identity name        : $packedName   (match=$okName)"
Write-Host "identity publisher   : $packedPublisher   (match=$okPublisher)"
Write-Host "publisher display    : $packedDisplay   (match=$okDisplay)"
Write-Host "version / arch       : $packedVersion / $arch"
Write-Host "runtime dependency   : $(if ($dependency) { $dependency } else { '(none, SDK bundled)' })"
Write-Host "payload files        : $fileCount"
if (-not ($okName -and $okPublisher -and $okDisplay)) { throw "packed identity does not match Partner Center" }
if ($packedVersion -ne "$Version.0") { throw "packed version $packedVersion is not $Version.0" }
if ($SelfContainedRuntime) {
    if ($dependency) { throw "packed manifest still declares $dependency although the SDK is bundled" }
} elseif ($dependency -ne "Microsoft.WindowsAppRuntime.2") {
    throw "packed manifest must declare Microsoft.WindowsAppRuntime.2 (got '$dependency')"
}

$msixHash = (Get-FileHash $msix -Algorithm SHA256).Hash.ToLower()
$uploadHash = (Get-FileHash $msixupload -Algorithm SHA256).Hash.ToLower()
Write-Host ("msix        {0,8:N1} MB  sha256 {1}" -f ((Get-Item $msix).Length / 1MB), $msixHash)
Write-Host ("msixupload  {0,8:N1} MB  sha256 {1}" -f ((Get-Item $msixupload).Length / 1MB), $uploadHash)

$sums = Join-Path $out "SHA256SUMS.txt"
$lines = @(
    "$msixHash  $(Split-Path -Leaf $msix)",
    "$uploadHash  $(Split-Path -Leaf $msixupload)"
)
[IO.File]::WriteAllLines($sums, $lines, (New-Object Text.UTF8Encoding $false))

if (-not $TestSign) { Write-Host "unsigned on purpose: Partner Center signs Store packages during certification." }
Remove-Item $check -Recurse -Force
Get-ChildItem $out -File | ForEach-Object { "{0,-46} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }
