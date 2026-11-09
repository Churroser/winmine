# Replaces every asset of an existing release with the files in artifacts\release.
# ASCII only: Windows PowerShell 5.1 parses BOM-less scripts as ANSI.
param([string]$Tag = "v1.2.0")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$cred = "protocol=https`nhost=github.com`n`n" | git credential fill
$token = ($cred | Select-String '^password=').Line -replace '^password=', ''
if (-not $token) { throw "no github token" }
$headers = @("Authorization: Bearer $token", "X-GitHub-Api-Version: 2022-11-28")
$repo = "Churroser/winmine"
$out = Join-Path $root "artifacts\release"
$tmp = Join-Path $root "artifacts\api-tmp"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

function Invoke-Api {
    param([string]$Method, [string]$Url, [string]$BodyFile, [string]$OutFile, [string]$ContentType = "application/json")
    $args = @("-s", "-m", "600", "-X", $Method, "-o", $OutFile, "-w", "%{http_code}")
    foreach ($h in $headers) { $args += @("-H", $h) }
    if ($ContentType) { $args += @("-H", "Content-Type: $ContentType") }
    if ($BodyFile) { $args += @("--data-binary", "@$BodyFile") }
    $args += $Url
    return (& curl.exe @args)
}

$relFile = Join-Path $tmp "rel.json"
$code = Invoke-Api -Method GET -Url "https://api.github.com/repos/$repo/releases/tags/$Tag" -OutFile $relFile
$release = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($relFile)) | ConvertFrom-Json
Write-Host ("release id={0} tag={1} assets={2}" -f $release.id, $release.tag_name, $release.assets.Count)

foreach ($asset in @($release.assets)) {
    $delFile = Join-Path $tmp "del.json"
    $code = Invoke-Api -Method DELETE -Url "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" -OutFile $delFile -ContentType $null
    Write-Host ("deleted {0} -> HTTP {1}" -f $asset.name, $code)
}

$assets = @("WinMine-1.2.0-x64.msix", "WinMine-1.2.0-win-x64.zip", "WinMine-msix-selfsigned.cer", "SHA256SUMS.txt")
$types = @{ ".msix" = "application/octet-stream"; ".zip" = "application/zip"; ".cer" = "application/x-x509-ca-cert"; ".txt" = "text/plain" }
foreach ($name in $assets) {
    $path = Join-Path $out $name
    if (-not (Test-Path $path)) { throw "missing $name" }
    $url = "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=$name"
    $assetOut = Join-Path $tmp ("up-" + ($name -replace '[^A-Za-z0-9.]', '_') + ".json")
    $code = Invoke-Api -Method POST -Url $url -BodyFile $path -OutFile $assetOut -ContentType $types[[IO.Path]::GetExtension($name)]
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($assetOut))
    if ($code -match '^20') {
        $a = $text | ConvertFrom-Json
        Write-Host ("uploaded {0} -> HTTP {1} size={2} digest={3}" -f $name, $code, $a.size, $a.digest)
    }
    else { Write-Host ("upload {0} -> HTTP {1}" -f $name, $code); Write-Host $text }
}

$checkFile = Join-Path $tmp "check.json"
$code = Invoke-Api -Method GET -Url "https://api.github.com/repos/$repo/releases/$($release.id)" -OutFile $checkFile
$checked = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($checkFile)) | ConvertFrom-Json
Write-Host "verify -> HTTP $code, assets=$($checked.assets.Count)"
foreach ($a in $checked.assets) {
    $local = "sha256:" + (Get-FileHash (Join-Path $out $a.name) -Algorithm SHA256).Hash.ToLower()
    Write-Host ("  {0,-32} match={1}" -f $a.name, ($a.digest -eq $local))
}
