# Publishes the 1.2.1 release and uploads its assets through the REST API.
# ASCII only: Windows PowerShell 5.1 parses BOM-less scripts as ANSI.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$cred = "protocol=https`nhost=github.com`n`n" | git credential fill
$token = ($cred | Select-String '^password=').Line -replace '^password=', ''
if (-not $token) { throw "no github token from git credential fill" }
$headers = @("Authorization: Bearer $token", "X-GitHub-Api-Version: 2022-11-28")
$repo = "Churroser/winmine"
$tag = "v1.2.1"
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
    $code = & curl.exe @args
    return $code
}

$bodyText = [IO.File]::ReadAllText((Join-Path $root "artifacts\release-notes-1.2.1.md"), [Text.Encoding]::UTF8)
$payload = [ordered]@{ tag_name = $tag; name = "Minesweeper 1.2.1 / WinUI 3"; body = $bodyText; draft = $false; prerelease = $false } |
    ConvertTo-Json -Depth 4
$bodyFile = Join-Path $tmp "release.json"
[IO.File]::WriteAllText($bodyFile, $payload, (New-Object Text.UTF8Encoding $false))

$respFile = Join-Path $tmp "release-response.json"
$code = Invoke-Api -Method POST -Url "https://api.github.com/repos/$repo/releases" -BodyFile $bodyFile -OutFile $respFile
$text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($respFile))
Write-Host "create release -> HTTP $code"
if ($code -notmatch '^20') { Write-Host $text; throw "release creation failed" }
$release = $text | ConvertFrom-Json
Write-Host ("release id={0} tag={1}" -f $release.id, $release.tag_name)
Write-Host ("html_url={0}" -f $release.html_url)

$assets = @("WinMine-1.2.1-x64.msix", "WinMine-1.2.1-win-x64.zip", "WinMine-msix-selfsigned.cer", "SHA256SUMS.txt")
$types = @{ ".msix" = "application/octet-stream"; ".zip" = "application/zip"; ".cer" = "application/x-x509-ca-cert"; ".txt" = "text/plain" }
foreach ($name in $assets) {
    $path = Join-Path $out $name
    if (-not (Test-Path $path)) { throw "missing asset $name" }
    $type = $types[[IO.Path]::GetExtension($name)]
    $url = "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=$name"
    $assetOut = Join-Path $tmp ("asset-" + ($name -replace '[^A-Za-z0-9.]', '_') + ".json")
    $code = Invoke-Api -Method POST -Url $url -BodyFile $path -OutFile $assetOut -ContentType $type
    $assetText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($assetOut))
    if ($code -match '^20') {
        $asset = $assetText | ConvertFrom-Json
        Write-Host ("uploaded {0} -> HTTP {1} size={2} digest={3}" -f $name, $code, $asset.size, $asset.digest)
    }
    else {
        Write-Host ("upload {0} -> HTTP {1}" -f $name, $code)
        Write-Host $assetText
    }
}

# Read the release back and compare the server-side digests with the local files.
$checkFile = Join-Path $tmp "release-check.json"
$code = Invoke-Api -Method GET -Url "https://api.github.com/repos/$repo/releases/$($release.id)" -OutFile $checkFile
$checked = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($checkFile)) | ConvertFrom-Json
Write-Host "verify -> HTTP $code, draft=$($checked.draft), assets=$($checked.assets.Count)"
foreach ($asset in $checked.assets) {
    $local = Join-Path $out $asset.name
    $localHash = "sha256:" + (Get-FileHash $local -Algorithm SHA256).Hash.ToLower()
    Write-Host ("  {0,-32} server={1} match={2}" -f $asset.name, $asset.digest, ($asset.digest -eq $localHash))
}
