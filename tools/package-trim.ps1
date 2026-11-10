# Strips payload that a Minesweeper build never touches, but that the Windows App SDK
# self-contained layout copies anyway. Every pattern below was verified by deleting it from a
# built layout and running both the self-test and the screenshot capture:
#
#   removed and still 112/112:  onnxruntime, DirectML, the Windows.AI / Vision / Workloads /
#                               Widgets / Search / Asg surfaces, workloads*.json, NPUDetect,
#                               PerceptiveStreaming, SessionHandleIPCProxyStub, DiaSymReader,
#                               createdump, *.pdb, System.Private.DataContractSerialization
#
#   MUST KEEP (these crash with 0xC000027B or 0xE0434352 when removed):
#       Microsoft.Windows.SDK.NET.dll   WinRT projection
#       WinUIEdit.dll                   XAML runtime dependency
#       System.Private.Xml.dll          PRI / XAML resource loading
#       System.Linq.Expressions.dll     XAML binding
#       System.Security.Cryptography.dll
#
# ASCII only: Windows PowerShell 5.1 parses BOM-less scripts as ANSI.

function Remove-OptionalPayload {
    param([Parameter(Mandatory = $true)][string]$Layout)

    $patterns = @(
        'onnxruntime.dll',
        'DirectML.dll',
        'Microsoft.ML.OnnxRuntime.dll',
        'Microsoft.Windows.AI.*',
        'Microsoft.Windows.Vision*',
        'Microsoft.Windows.Workloads*',
        'workloads*.json',
        'NPUDetect.dll',
        'PerceptiveStreaming.dll',
        'Microsoft.Asg.*',
        'Microsoft.Windows.Search.dll',
        'Microsoft.Windows.Widgets*',
        'Microsoft.Windows.Private*',
        'SessionHandleIPCProxyStub.dll',
        'Microsoft.DiaSymReader.Native.amd64.dll',
        'createdump.exe',
        '*.pdb',
        'System.Private.DataContractSerialization.dll'
    )

    $before = (Get-ChildItem $Layout -Recurse -File | Measure-Object Length -Sum).Sum
    $removed = 0
    $count = 0
    foreach ($pattern in $patterns) {
        Get-ChildItem -Path $Layout -Filter $pattern -Recurse -File -ErrorAction SilentlyContinue |
            ForEach-Object {
                $removed += $_.Length
                $count++
                Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
            }
    }
    $after = (Get-ChildItem $Layout -Recurse -File | Measure-Object Length -Sum).Sum
    Write-Host ("trimmed {0} files, {1:N1} MB -> {2:N1} MB" -f $count, ($before / 1MB), ($after / 1MB))
}
