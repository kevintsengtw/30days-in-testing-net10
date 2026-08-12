# Day 17: System.IO.Abstractions 檔案系統測試

## 專案描述

這個專案示範如何使用 System.IO.Abstractions 進行檔案系統相關的單元測試。透過抽象層將檔案系統操作與具體實作分離，讓我們能夠輕鬆地進行測試，而不需要實際操作檔案系統。

## 學習目標

- 理解檔案系統抽象的重要性
- 學習使用 System.IO.Abstractions 套件
- 掌握 MockFileSystem 的使用技巧
- 實作檔案系統相關服務的測試策略
- 整合 NSubstitute 進行複雜情境模擬

## 專案結構

```text
Day17.FileSystemTesting/
├── Day17.FileSystemTesting.sln
├── README.md
├── src/
│   └── Day17.FileSystemTesting.Core/
│       ├── Day17.FileSystemTesting.Core.csproj
│       ├── GlobalUsings.cs
│       ├── ConfigurationService.cs          # 設定檔管理服務
│       ├── FileManagerService.cs            # 檔案操作管理服務
│       ├── FilePermissionService.cs         # 檔案權限檢查服務
│       ├── StreamProcessorService.cs        # 檔案串流處理服務
│       └── ConfigManagerService.cs          # 整合設定管理服務
└── tests/
    └── Day17.FileSystemTesting.Tests/
        ├── Day17.FileSystemTesting.Tests.csproj
        ├── GlobalUsings.cs
        ├── ConfigurationServiceTests.cs
        ├── FileManagerServiceTests.cs
        ├── FilePermissionServiceTests.cs
        ├── StreamProcessorServiceTests.cs
        └── ConfigManagerServiceTests.cs
```

## 使用的套件版本

### 核心專案 (Day17.FileSystemTesting.Core)

- **.NET 10.0 (net10.0)**: 目標框架
- **System.IO.Abstractions 22.2.0**: 檔案系統抽象套件

### 測試專案 (Day17.FileSystemTesting.Tests)

- **xunit.v3.mtp-v2 3.2.2**: 單元測試框架（Microsoft.Testing.Platform 模式）
- **AwesomeAssertions 9.5.0**: 斷言庫
- **NSubstitute 6.2.0**: Mock 框架
- **Microsoft.Extensions.TimeProvider.Testing 10.9.0**: 可控制時間的測試工具
- **System.IO.Abstractions.TestingHelpers 22.2.0**: 測試輔助工具
- **Microsoft.Testing.Extensions.TrxReport 2.3.3**: TRX 測試報告

## 核心功能

### 1. ConfigurationService

提供設定檔的讀取與寫入功能：

- 文字設定檔處理
- JSON 設定檔序列化/反序列化
- 錯誤處理和預設值回退
- 自動目錄建立

### 2. FileManagerService

實作檔案管理相關功能：

- 檔案複製到指定目錄
- 檔案備份（含可測試的時間戳記）
- 舊備份檔案清理
- 檔案資訊查詢

### 3. FilePermissionService

檢查檔案和目錄的存取權限：

- 檔案讀取權限檢查
- 檔案寫入權限檢查
- 目錄寫入權限檢查
- 權限摘要報告

### 4. StreamProcessorService

處理檔案串流操作：

- 文字檔案逐行處理
- 檔案雜湊值計算（MD5）
- 檔案內容比較
- 檔案統計資訊（行數、字數、字元數）

### 5. ConfigManagerService

整合的設定管理方案：

- 應用程式設定管理
- 設定備份與還原
- 備份歷史管理
- 完整的設定生命週期

## 執行方式

### 建置專案

```powershell
dotnet build Day17.FileSystemTesting.sln
```

### 執行測試

```powershell
dotnet test --solution Day17.FileSystemTesting.sln
```

### 執行特定測試類別

```powershell
dotnet test --solution Day17.FileSystemTesting.sln --filter-class "Day17.FileSystemTesting.Tests.ConfigurationServiceTests"
dotnet test --solution Day17.FileSystemTesting.sln --filter-class "Day17.FileSystemTesting.Tests.StreamProcessorServiceTests"
```

## 重點學習內容

### 1. System.IO.Abstractions 基礎

- **IFileSystem 介面**: 檔案系統抽象的核心
- **MockFileSystem**: 記憶體中的檔案系統模擬
- **實際檔案系統**: `new FileSystem()` 用於生產環境

### 2. 測試策略

- **正常情境測試**: 驗證功能按預期運作
- **邊界值測試**: 空檔案、大檔案、特殊字元檔名等
- **例外情境測試**: 檔案不存在、權限不足、磁碟空間不足
- **整合測試**: 完整的業務流程驗證
- **記憶體效率測試**: 驗證大量資料處理的記憶體使用

### 3. Mock 技巧

- **MockFileSystem 設定**: 建立虛擬檔案和目錄
- **檔案屬性模擬**: 建立時間、修改時間、檔案大小、權限
- **NSubstitute 整合**: 模擬複雜的錯誤情境

### 4. 測試設計模式

- **3A 模式**: Arrange、Act、Assert
- **Theory 測試**: 參數化測試多種情境
- **測試命名**: 清楚描述測試情境和預期結果

### 5. 實務應用情境

- **設定檔管理**: 應用程式設定的載入、儲存、備份
- **檔案處理管道**: 批次處理、轉換、驗證
- **權限檢查**: 安全性驗證、預防性檢查
- **檔案系統監控**: 檔案變更追蹤、統計報告

## 測試重點特色

1. **完整的錯誤處理測試**: 涵蓋各種例外情況
2. **真實情境模擬**: 使用實際的檔案操作情境
3. **效能考量**: 檔案大小比較的最佳化策略
4. **資料驗證**: 雜湊值比較確保檔案完整性
5. **使用者體驗**: 清楚的錯誤訊息和狀態回報

## 延伸思考

1. **如何處理大檔案**: 串流處理 vs 記憶體載入
2. **跨平台相容性**: 不同作業系統的路徑處理
3. **並行處理**: 多執行緒檔案操作的安全性
4. **監控與日誌**: 檔案操作的追蹤與稽核
5. **快取策略**: 檔案內容和中繼資料的快取機制

這個專案展示了如何透過抽象化讓檔案系統操作變得可測試，並提供了豐富的測試案例作為參考。透過這些實作，你可以建立穩定可靠的檔案處理服務。
