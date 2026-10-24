param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts"))
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "publish\win-x64"))
$mcpDirectory = [System.IO.Path]::GetFullPath((Join-Path $publishDirectory "mcp"))
$releaseDirectory = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "releases"))

foreach ($path in @($publishDirectory, $releaseDirectory)) {
    if (-not $path.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the artifacts directory: $path"
    }
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $mcpDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

dotnet restore (Join-Path $projectRoot "Pinlist.sln")
if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }

dotnet test (Join-Path $projectRoot "tests\Pinlist.Tests\Pinlist.Tests.csproj") -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

dotnet publish (Join-Path $projectRoot "src\Pinlist.Desktop\Pinlist.Desktop.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "Desktop publish failed." }

dotnet publish (Join-Path $projectRoot "src\Pinlist.Integration.Mcp\Pinlist.Integration.Mcp.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false `
    -o $mcpDirectory
if ($LASTEXITCODE -ne 0) { throw "MCP publish failed." }

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Velopack tool restore failed." }

dotnet tool run vpk pack `
    --packId "MrSlipperCN.Pinlist" `
    --packVersion $Version `
    --packDir $publishDirectory `
    --mainExe "Pinlist.Desktop.exe" `
    --packTitle "Pinlist" `
    --packAuthors "MrSlipperCN" `
    --icon (Join-Path $projectRoot "src\Pinlist.Desktop\Assets\pinlist.ico") `
    --runtime win-x64 `
    --outputDir $releaseDirectory
if ($LASTEXITCODE -ne 0) { throw "Velopack packaging failed." }

Write-Host "Velopack release created in: $releaseDirectory"
