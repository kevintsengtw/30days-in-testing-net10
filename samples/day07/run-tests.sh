#!/bin/bash
# Day 07 範例專案測試執行腳本（xUnit v3 + Microsoft Testing Platform）

set -e
solution="Day07.DependencyReplacement.sln"

echo "=== Day 07 範例專案測試執行 ==="
echo ""

echo "1. 還原套件..."
dotnet restore "$solution"

echo ""
echo "2. 建置專案..."
dotnet build "$solution" -c Release --no-restore

echo ""
echo "3. 執行測試並輸出 TRX 報告..."
dotnet test --solution "$solution" -c Release --no-build \
    --report-trx --report-trx-filename day07.trx

echo ""
echo "4. 產生程式碼覆蓋率報告（Microsoft Code Coverage）..."
dotnet test --solution "$solution" -c Release --no-build \
    --coverage --coverage-output-format cobertura --coverage-output day07.cobertura.xml

echo ""
echo "✅ 所有步驟完成！"
echo ""
echo "📄 報告輸出位置：tests/Day07.Tests/bin/Release/net10.0/TestResults/"
echo "   - day07.trx（測試結果）"
echo "   - day07.cobertura.xml（覆蓋率）"
echo ""
echo "📁 專案結構："
echo "├── src/"
echo "│   ├── Day07.Legacy/          # 不可測試的 Legacy Code"
echo "│   └── Day07.Refactored/      # 重構後的可測試程式碼"
echo "└── tests/"
echo "    └── Day07.Tests/           # 單元測試"
echo ""
echo "🎯 測試重點："
echo "- ✅ 成功備份情境"
echo "- ✅ 檔案不存在處理"
echo "- ✅ 檔案過大檢查"
echo "- ✅ 資料庫例外處理"
echo "- ✅ 時間戳功能"
echo "- ✅ 依賴互動驗證"
echo "- ✅ 組裝 smoke test 範例"
