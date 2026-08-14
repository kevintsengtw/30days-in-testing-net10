# xUnit v2 → v3 唯讀遷移盤點工具
[CmdletBinding()]
param(
    [Parameter()]
    [string]$ProjectPath = "."
)

$ErrorActionPreference = "Stop"
$resolvedPath = (Resolve-Path -LiteralPath $ProjectPath).Path
$projectFiles = @(Get-ChildItem -LiteralPath $resolvedPath -Filter "*.csproj" -Recurse)

if ($projectFiles.Count -eq 0) {
    throw "在 $resolvedPath 找不到 .csproj。"
}

foreach ($projectFile in $projectFiles) {
    $projectText = Get-Content -LiteralPath $projectFile.FullName -Raw
    $sourceFiles = @(Get-ChildItem -LiteralPath $projectFile.DirectoryName -Filter "*.cs" -Recurse)

    Write-Host "Project: $($projectFile.FullName)"
    Write-Host "  SDK style: $($projectText -match '<Project Sdk=')"
    Write-Host "  xUnit v2 package: $($projectText -match 'Include=\"xunit\"')"
    Write-Host "  xunit.abstractions: $($projectText -match 'xunit\.abstractions')"
    Write-Host "  OutputType Exe: $($projectText -match '<OutputType>Exe</OutputType>')"

    $asyncVoidMatches = @(
        foreach ($sourceFile in $sourceFiles) {
            Select-String -LiteralPath $sourceFile.FullName -Pattern 'async\s+void\s+\w+\s*\(' |
                ForEach-Object { "{0}:{1}" -f $_.Path, $_.LineNumber }
        }
    )

    Write-Host "  async void methods: $($asyncVoidMatches.Count)"
    $asyncVoidMatches | ForEach-Object { Write-Host "    $_" }
}

Write-Host ""
Write-Host "這是唯讀報告。請依 README 與 UPGRADE_CHECKLIST.md 人工完成 runner、套件及 API 遷移。"
