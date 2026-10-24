param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$version = Get-Date -Format "yyyyMMdd-HHmmss"
$outputDirectory = Join-Path $projectRoot "runtime\mcp\$version"
$project = Join-Path $projectRoot "src\Pinlist.Integration.Mcp\Pinlist.Integration.Mcp.csproj"

dotnet publish $project -c $Configuration -o $outputDirectory --no-self-contained
if ($LASTEXITCODE -ne 0) { throw "Pinlist MCP publish failed." }

$server = Join-Path $outputDirectory "Pinlist.Integration.Mcp.dll"
codex mcp remove pinlist 2>$null
codex mcp add pinlist -- dotnet $server
if ($LASTEXITCODE -ne 0) { throw "Pinlist MCP registration failed." }

Write-Host "Pinlist MCP registered from: $server"
Write-Host "Open a new Codex session to load the updated tool manifest."
