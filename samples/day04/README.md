# Day 04 - AwesomeAssertions 基礎應用範例專案

這個專案展示了 Day 04 文章中所有 AwesomeAssertions 的使用範例。

## 專案結構

```
Day04.AwesomeAssertions.sln
├── src/
│   └── Day04.Domain/                    # 領域模型和服務
│       ├── Models/                      # 資料模型
│       │   ├── User.cs                  # 使用者模型
│       │   ├── Order.cs                 # 訂單模型
│       │   └── Common.cs                # 共用模型
│       └── Services/                    # 業務服務
│           ├── UserService.cs           # 使用者服務
│           ├── OrderService.cs          # 訂單服務
│           └── UtilityServices.cs       # 工具服務
└── tests/
    └── Day04.Tests/                     # 測試專案
        ├── BasicAssertions/             # 基本斷言範例
        │   ├── ObjectAssertionTests.cs  # 物件斷言
        │   ├── StringAssertionTests.cs  # 字串斷言
        │   ├── NumericAssertionTests.cs # 數值斷言
        │   ├── CollectionAssertionTests.cs # 集合斷言
        │   ├── ExceptionAssertionTests.cs # 例外斷言
        │   ├── AsyncAssertionTests.cs   # 非同步斷言
        │   ├── ObjectComparisonTests.cs # 物件比對
        │   └── ComplexObjectComparisonTests.cs # 複雜物件比對
        └── PracticalExamples/           # 實戰範例
            ├── UserServiceTests.cs      # 使用者服務測試
            ├── ReadableAssertionTests.cs # 可讀性斷言
            ├── DomainSpecificAssertionPatterns.cs # 領域特定斷言
            └── AssertionStyleComparison.cs # 斷言風格對比
```

## 開始使用

### 前置需求

- .NET 10 SDK
- Visual Studio 2022 或 VS Code

### 執行測試

```bash
# 進入專案目錄
cd samples/day04

# 還原套件
dotnet restore

# 建置專案
dotnet build

# 執行所有測試
dotnet test --solution Day04.AwesomeAssertions.sln

# 執行特定測試類別（xUnit v3 MTP 篩選語法）
dotnet test --solution Day04.AwesomeAssertions.sln --filter-class "Day04.Tests.BasicAssertions.ObjectAssertionTests"

# 執行測試並輸出 TRX 報告
dotnet test --solution Day04.AwesomeAssertions.sln --report-trx --report-trx-filename day04.trx
```

## 技術規格

- **.NET**: 10.0
- **測試框架**: xunit.v3.mtp-v2 3.2.2（Microsoft Testing Platform 模式）
- **斷言庫**: AwesomeAssertions 9.5.0
- **測試數量**: 40 個（12 個測試類別，全數通過）

## 測試範例說明

### 基本斷言範例 (BasicAssertions/)

1. **ObjectAssertionTests.cs** - 展示物件基本斷言
   - 空值檢查
   - 型別驗證
   - 屬性值比對

2. **StringAssertionTests.cs** - 展示字串斷言
   - 內容檢查
   - 正規表達式匹配
   - 大小寫忽略比較

3. **NumericAssertionTests.cs** - 展示數值斷言
   - 範圍檢查
   - 浮點數精度比較
   - 特殊值處理

4. **CollectionAssertionTests.cs** - 展示集合斷言
   - 基本集合操作
   - 順序和唯一性檢查
   - 複雜物件集合

5. **ExceptionAssertionTests.cs** - 展示例外斷言
   - 基本例外捕捉
   - 例外訊息驗證
   - 例外類型檢查

6. **AsyncAssertionTests.cs** - 展示非同步斷言
   - Task 完成狀態
   - 非同步例外處理

7. **ObjectComparisonTests.cs** - 展示物件比對
   - 深度物件比較
   - 屬性排除策略

8. **ComplexObjectComparisonTests.cs** - 展示複雜物件比對
   - 巢狀物件結構
   - 部分屬性比較

### 實戰範例 (PracticalExamples/)

1. **UserServiceTests.cs** - 展示服務層測試
   - 3A 模式 (Arrange-Act-Assert)
   - Theory 資料驅動測試
   - 業務邏輯驗證

2. **ReadableAssertionTests.cs** - 展示可讀性最佳實踐
   - 鏈式斷言
   - 有意義的錯誤訊息
   - 邏輯分組

3. **DomainSpecificAssertionPatterns.cs** - 展示領域特定斷言
   - 業務規則驗證
   - API 回應檢查
   - 複合條件斷言

4. **AssertionStyleComparison.cs** - 展示斷言風格對比
   - 傳統 Assert vs AwesomeAssertions
   - 錯誤訊息比較
   - 可讀性差異

## 關鍵特性展示

### AwesomeAssertions 優勢
- **流暢語法**: `user.Should().NotBeNull().And.BeOfType<User>()`
- **詳細錯誤訊息**: 失敗時提供完整的上下文資訊
- **方法鏈結**: 支援連續斷言操作
- **強型別支援**: 編譯時型別檢查

### 實際應用技巧
- **測試命名**: 遵循 `[方法]_[情境]_[預期結果]` 模式
- **斷言分組**: 相關斷言邏輯分組
- **錯誤訊息**: 提供有意義的失敗原因
- **業務規則**: 將複雜業務邏輯轉換為清晰的斷言

## 學習目標

透過這個範例專案，你將學會：

1. 掌握各種資料類型的基本斷言語法
2. 理解流暢斷言的優勢和最佳實踐
3. 學會處理複雜物件比對和例外情況
4. 建立可讀性高、維護性好的測試程式碼
5. 應用領域特定的斷言模式

## 相關文章

請參考 [Day 04 - AwesomeAssertions 基礎應用與遷移策略](../../Day04.md) 獲得完整的理論基礎和最佳實踐指南。

## 提示

- 執行測試時注意觀察錯誤訊息的詳細程度
- 嘗試修改測試條件來體驗不同的斷言功能
- 比較傳統 Assert 和 AwesomeAssertions 的差異
- 實踐測試命名和組織的最佳實踐
