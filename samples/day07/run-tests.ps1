# Day 07 範例專案測試執行腳本（xUnit v3 + Microsoft Testing Platform）

$ErrorActionPreference = "Stop"
$solution = "Day07.DependencyReplacement.sln"

Write-Host "=== Day 07 範例專案測試執行 ===" -ForegroundColor Green
Write-Host ""

Write-Host "1. 還原套件..." -ForegroundColor Yellow
dotnet restore $solution
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 套件還原失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "2. 建置專案..." -ForegroundColor Yellow
dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 建置失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "3. 執行測試並輸出 TRX 報告..." -ForegroundColor Yellow
dotnet test --solution $solution -c Release --no-build `
    --report-trx --report-trx-filename day07.trx
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 測試失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "4. 產生程式碼覆蓋率報告（Microsoft Code Coverage）..." -ForegroundColor Yellow
dotnet test --solution $solution -c Release --no-build `
    --coverage --coverage-output-format cobertura --coverage-output day07.cobertura.xml
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 覆蓋率產生失敗" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ 所有步驟完成！" -ForegroundColor Green
Write-Host ""
Write-Host "📄 報告輸出位置：tests/Day07.Tests/bin/Release/net10.0/TestResults/" -ForegroundColor Cyan
Write-Host "   - day07.trx（測試結果）"
Write-Host "   - day07.cobertura.xml（覆蓋率）"
Write-Host ""
Write-Host "📁 專案結構：" -ForegroundColor Cyan
Write-Host "├── src/"
Write-Host "│   ├── Day07.Legacy/          # 不可測試的 Legacy Code"
Write-Host "│   └── Day07.Refactored/      # 重構後的可測試程式碼"
Write-Host "└── tests/"
Write-Host "    └── Day07.Tests/           # 單元測試"
Write-Host ""
Write-Host "🎯 測試重點：" -ForegroundColor Cyan
Write-Host "- ✅ 成功備份情境"
Write-Host "- ✅ 檔案不存在處理"
Write-Host "- ✅ 檔案過大檢查"
Write-Host "- ✅ 資料庫例外處理"
Write-Host "- ✅ 時間戳功能"
Write-Host "- ✅ 依賴互動驗證"
Write-Host "- ✅ 組裝 smoke test 範例"
