#Requires -Version 5.1
<#
.SYNOPSIS
    Sets description / homepage / topics for Churroser/winmine through the GitHub REST API.

.DESCRIPTION
    Two encoding traps this script is built around:
      1. Windows PowerShell 5.1 parses BOM-less .ps1 files as ANSI, so any non-ASCII literal in
         the script body is corrupted before it reaches the API. Everything localized is read
         from a UTF-8 file instead (tools\repo-description.txt).
      2. Child-process stdout is decoded with the console code page, which mangles UTF-8
         responses. HTTP bodies are therefore written with curl -o and re-read as raw bytes.

    Token resolution order: -Token, $env:GITHUB_TOKEN, token file (first line, trimmed).

.NOTES
    Requires a fine-grained PAT with "Administration: Read and write" on the repository.
#>
param(
    [string]$Owner = 'Churroser',
    [string]$Repository = 'winmine',
    [string]$Token,
    [string]$TokenFile = "$env:LOCALAPPDATA\WinMine\.github-token",
    [string]$DescriptionFile = "$PSScriptRoot\repo-description.txt",
    [string]$Homepage = '',
    [string[]]$Topics = @('winui3', 'windows-app-sdk', 'minesweeper', 'csharp', 'dotnet', 'fluent-design', 'desktop-app', 'game')
)

$ErrorActionPreference = 'Stop'

if (-not $Token) {
    if ($env:GITHUB_TOKEN) {
        $Token = $env:GITHUB_TOKEN
    }
    elseif (Test-Path -LiteralPath $TokenFile) {
        $Token = (Get-Content -LiteralPath $TokenFile -TotalCount 1).Trim()
    }
}

if (-not $Token) {
    throw ('No token. Pass -Token, set $env:GITHUB_TOKEN, or write one line to ' + $TokenFile)
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$api = "https://api.github.com/repos/$Owner/$Repository"
$work = Join-Path $env:TEMP ("ghm-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work -Force | Out-Null
$bodyFile = Join-Path $work 'body.json'
$reqFile = Join-Path $work 'req.json'

function Invoke-GitHub {
    param([string]$Method, [string]$Url, [object]$Payload, [string]$Accept = 'application/vnd.github+json')

    if (Test-Path -LiteralPath $bodyFile) { Remove-Item -LiteralPath $bodyFile -Force }

    $curlArgs = @(
        '-s', '-m', '30',
        '-X', $Method,
        '-H', "Authorization: Bearer $Token",
        '-H', 'User-Agent: WinMine-CLI',
        '-H', "Accept: $Accept",
        '-H', 'X-GitHub-Api-Version: 2022-11-28',
        '-o', $bodyFile,
        '-w', '%{http_code}'
    )

    if ($null -ne $Payload) {
        [System.IO.File]::WriteAllText($reqFile, ($Payload | ConvertTo-Json -Compress -Depth 5), $utf8)
        $curlArgs += @('-H', 'Content-Type: application/json; charset=utf-8', '--data-binary', "@$reqFile")
    }

    $statusText = (curl.exe @curlArgs $Url) -join ''
    $code = 0
    if ($statusText -match '(\d{3})') { $code = [int]$Matches[1] }

    $body = ''
    if (Test-Path -LiteralPath $bodyFile) {
        $body = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($bodyFile))
    }

    [pscustomobject]@{ Status = $code; Body = $body }
}

function Get-Utf8Hex {
    param([string]$Text)
    (([System.Text.Encoding]::UTF8.GetBytes($Text) | ForEach-Object { $_.ToString('x2') }) -join '')
}

try {
    if (Test-Path -LiteralPath $DescriptionFile) {
        $Description = ([System.IO.File]::ReadAllText($DescriptionFile, $utf8)).Trim()
    }
    else {
        $Description = 'WinUI 3 Minesweeper clone'
    }

    Write-Host ("description local : len=" + $Description.Length + " hex=" + (Get-Utf8Hex $Description).Substring(0, 32) + '...')

    $who = Invoke-GitHub -Method GET -Url 'https://api.github.com/user'
    if ($who.Status -ne 200) {
        Write-Host ("AUTH FAILED (HTTP " + $who.Status + "): " + $who.Body)
        exit 1
    }
    Write-Host ("authenticated as: " + ($who.Body | ConvertFrom-Json).login)

    $repo = Invoke-GitHub -Method GET -Url $api
    if ($repo.Status -ne 200) {
        Write-Host ("REPO LOOKUP FAILED (HTTP " + $repo.Status + "): " + $repo.Body)
        exit 1
    }
    $info = $repo.Body | ConvertFrom-Json
    Write-Host ("repo: " + $info.full_name + "  private=" + $info.private + "  default=" + $info.default_branch)

    $payload = @{ description = $Description }
    if ($Homepage) { $payload['homepage'] = $Homepage }

    $updated = Invoke-GitHub -Method PATCH -Url $api -Payload $payload
    Write-Host ("PATCH description -> HTTP " + $updated.Status)

    $put = Invoke-GitHub -Method PUT -Url "$api/topics" -Payload @{ names = $Topics }
    Write-Host ("PUT topics -> HTTP " + $put.Status)

    $final = (Invoke-GitHub -Method GET -Url $api).Body | ConvertFrom-Json
    $remote = "$($final.description)"

    Write-Host ''
    Write-Host ("description remote: len=" + $remote.Length + " hex=" + (Get-Utf8Hex $remote).Substring(0, 32) + '...')
    Write-Host ("description match : " + [string]($remote -ceq $Description))
    Write-Host ("homepage          : " + $final.homepage)
    Write-Host ("topics            : " + (($final.topics) -join ', '))

    $outFile = Join-Path $work 'final.txt'
    [System.IO.File]::WriteAllText($outFile, $remote, $utf8)
    Write-Host ("description file  : " + $outFile)
}
finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
