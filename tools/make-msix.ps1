# Builds, packages and signs the MSIX for WinMine, optionally installs it for a smoke test.
# ASCII only on purpose: Windows PowerShell 5.1 parses BOM-less scripts as ANSI.
#
#   powershell -File tools\make-msix.ps1                  # build + package + sign + zip
#   powershell -File tools\make-msix.ps1 -SkipBuild       # reuse the existing layout
#   powershell -File tools\make-msix.ps1 -TestInstall     # also trust, install, launch, uninstall
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.1.0",
    [string]$OutputDir = "artifacts\release",
    [switch]$SkipBuild,
    [switch]$TestInstall,
    [switch]$KeepInstalled,
    [string]$CertPassword = "winmine-msix"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$kit = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64"
$makeappx = Join-Path $kit "makeappx.exe"
$signtool = Join-Path $kit "signtool.exe"
foreach ($tool in @($makeappx, $signtool)) {
    if (-not (Test-Path $tool)) { throw "missing SDK tool: $tool" }
}

$layout = Join-Path $root "artifacts\msix-layout"
$out = Join-Path $root $OutputDir
New-Item -ItemType Directory -Force -Path $out | Out-Null

if (-not $SkipBuild) {
    if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
    Write-Host "building self-contained layout ..."
    # WindowsPackageType=MSIX keeps the WinAppSDK bootstrapper out of the startup path;
    # combined with the self-contained payload the same files run packaged and unpackaged.
    & dotnet build "src\WinMine\WinMine.csproj" -c $Configuration `
        -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=MSIX `
        -p:Version=$Version -o $layout
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
}

foreach ($required in @("WinMine.exe", "WinMine.pri", "Microsoft.ui.xaml.dll")) {
    if (-not (Test-Path (Join-Path $layout $required))) { throw "layout is incomplete, missing $required" }
}

Copy-Item "packaging\AppxManifest.xml" (Join-Path $layout "AppxManifest.xml") -Force
$assetsDir = Join-Path $layout "Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
Copy-Item "packaging\Assets\*.png" $assetsDir -Force

$manifest = [IO.File]::ReadAllText((Join-Path $layout "AppxManifest.xml"), [Text.Encoding]::UTF8)
if ($manifest -notmatch ('Version="' + [regex]::Escape($Version) + '\.0"')) {
    Write-Warning "AppxManifest.xml Identity Version does not read $Version.0 - edit packaging\AppxManifest.xml"
}

$msix = Join-Path $out "WinMine-$Version-x64.msix"
if (Test-Path $msix) { Remove-Item $msix -Force }
Write-Host "packing $msix ..."
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)" }

# self-signed certificate, reused between runs
$subject = "CN=Churroser"
$pfx = Join-Path $out "WinMine-msix-selfsigned.pfx"
$cer = Join-Path $out "WinMine-msix-selfsigned.cer"
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending | Select-Object -First 1

if (-not $cert) {
    Write-Host "creating self-signed code signing certificate ..."
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
        -FriendlyName "WinMine MSIX signing" `
        -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(5)
}

$secure = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $secure | Out-Null
Export-Certificate -Cert $cert -FilePath $cer -Type CERT | Out-Null

Write-Host "signing ..."
& $signtool sign /fd SHA256 /f $pfx /p $CertPassword $msix
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed ($LASTEXITCODE)" }

$zip = Join-Path $out "WinMine-$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "zipping portable build ..."
Compress-Archive -Path (Join-Path $layout "*") -DestinationPath $zip -CompressionLevel Optimal

if (Test-Path "packaging\README-portable.txt") {
    Copy-Item "packaging\README-portable.txt" (Join-Path $out "WinMine-$Version-portable-readme.txt") -Force
}

Get-ChildItem $out -File | Where-Object { $_.Extension -ne ".pfx" } |
    ForEach-Object { "{0,-44} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB) }

$trusted = "Cert:\CurrentUser\TrustedPeople"

function Test-MsixSignature([string]$path) {
    # signtool writes its verdict to stderr, which would be a terminating error under
    # $ErrorActionPreference = 'Stop'; capture the exit code instead.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & $signtool verify /pa $path 2>&1 | Out-String
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previous
    return @($code, $output)
}

$result = Test-MsixSignature $msix
if ($result[0] -eq 0) {
    Write-Host "signature verified by signtool"
}
else {
    Write-Host "signtool verify reports an untrusted root (expected for a self-signed certificate)."
    Write-Host "Windows only needs the certificate in TrustedPeople, which the install step below does."
}

if ($TestInstall) {
    Write-Host "trusting the self-signed certificate for the current user ..."
    $already = Get-ChildItem $trusted | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
    if (-not $already) { Import-Certificate -FilePath $cer -CertStoreLocation $trusted | Out-Null }

    Write-Host "installing $msix ..."
    Add-AppxPackage -Path $msix -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name "Churroser.WinMine"
    $installed | Select-Object Name, Version, PackageFullName, InstallLocation | Format-List

    $alias = Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\WinMine.exe"
    if (Test-Path $alias) {
        $report = Join-Path $root "artifacts\packaged-selftest.txt"
        if (Test-Path $report) { Remove-Item $report -Force }
        Write-Host "running the packaged app through its execution alias ..."
        $proc = Start-Process -FilePath $alias -ArgumentList "--selftest `"$report`"" -PassThru
        $proc.WaitForExit(180000) | Out-Null
        Write-Host ("packaged self-test exit code: {0}" -f $proc.ExitCode)
        if (Test-Path $report) {
            Get-Content $report -Encoding utf8 | Select-String -Pattern "FAIL|RESULT" | ForEach-Object { $_.Line }
        }
        else {
            Write-Warning "the packaged app did not write $report"
        }
    }
    else {
        Write-Warning "execution alias not found at $alias"
    }

    if (-not $KeepInstalled) {
        Write-Host "uninstalling ..."
        Get-AppxPackage -Name "Churroser.WinMine" | Remove-AppxPackage
    }
}
