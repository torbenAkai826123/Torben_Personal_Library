# 純 C# 測試 (dotnet test)

不依賴 Unity 的程式可以在 Unity 外部用 `dotnet test` 測試。Unity 與 dotnet 編譯的是同一份原始碼，不另存副本：

- 函式庫 Core：`Assets/[TorbenJuniorUtility]/Scripts/Core/`
- 範例 Samples 中不依賴 Unity 的部分：`Assets/[TorbenJuniorUtility]/Scripts/Samples/`

## 環境

| 項目 | 版本 |
|---|---|
| .NET SDK | 10.0.401 (2026-10-01 驗證；本機另有 10.0.400) |
| Core 目標框架 | `netstandard2.1`，`LangVersion 9.0` (對齊 Unity 6 的 API 相容層級與 C# 版本) |
| 測試專案 | `net10.0`，NUnit 3.14.0、NUnit3TestAdapter 4.6.0、Microsoft.NET.Test.Sdk 17.12.0 |

測試套件第一次執行時會從 nuget.org 還原到使用者的 NuGet 快取。

## 執行

在倉庫根目錄執行。路徑含空白與 `[TorbenJuniorUtility]`，一律加引號。

```powershell
dotnet --version                       # 確認 SDK
dotnet test "dotnet/TorbenJunior.sln"  # 建置並執行全部測試, 退出碼 0 表示通過
```

## 結構

```text
Assets/[TorbenJuniorUtility]/Scripts/
├─ Core/       Torben.Core.asmdef (noEngineReferences)
├─ Runtime/    Torben.Runtime.asmdef → Core
├─ Editor/     Torben.Editor.asmdef (僅 Editor)
├─ Tests/
│  ├─ EditMode/  Torben.Tests.EditMode.asmdef
│  └─ PlayMode/  Torben.Tests.PlayMode.asmdef
└─ Samples/Calculator/  Torben.Samples.asmdef (noEngineReferences)
   ├─ Runtime/  Torben.Samples.Runtime.asmdef
   ├─ Editor/   Torben.Samples.Editor.asmdef
   └─ Tests/    Torben.Samples.Tests.asmdef (EditMode)
dotnet/
├─ Torben.Core/                       Compile Include 指向 Assets 內的 Core
├─ Torben.Core.Tests/                 NUnit 測試 (目前沒有測試)
├─ Torben.Samples/       Compile Include 指向 Samples, 排除 Runtime/Editor/Tests 子資料夾
├─ Torben.Samples.Tests/ 計算機的 NUnit 測試, 作為工具鏈的冒煙測試
└─ TorbenJunior.sln
```

- `Torben.Core.csproj` 以 `**/*.cs` 收進 Core 資料夾所有檔案；在 Core 新增 `.cs` 不需改 csproj。
- `Torben.Samples.csproj` 收進 `Samples/` 下所有檔案，但排除 `Runtime/`、`Editor/`、`Tests/` 子資料夾 (那些屬於需要 Unity 的 asmdef)。新增範例時沿用相同的資料夾分法即可。
- dotnet 測試放在 `dotnet/` 底下，不放進 `Assets/`，Unity 不會編譯它們。
- `Torben.Core.Tests` 目前沒有測試，`dotnet test` 會顯示「未提供任何測試」，但退出碼仍為 0。
- `bin/`、`obj/` 已列入 `.gitignore`；`dotnet/` 底下的 `.csproj`、`.sln` 例外保留在版控中。

## Core 與 Samples 的限制

- 兩邊都不允許引用 `UnityEngine`／`UnityEditor`：Unity 由 asmdef 的 `noEngineReferences` 擋下，dotnet 則因沒有 Unity 組件而無法編譯。在 Core 加入 `using UnityEngine;` 時，兩邊都會出現 `CS0246` (2026-10-01 驗證)。
- 只能用 .NET Standard 2.1 與 C# 9 的功能；dotnet 端已用相同設定，超出的寫法會在 `dotnet test` 時就編譯失敗。
- dotnet 測試只驗證邏輯正確性；GC 與效能數字以 Unity Performance Testing 為準。

## 命名與 Unity 驗證

- 自有 namespace 與組件統一以 `Torben` 為根。計算機 Model 與 View 使用 `Torben.Calculator`, Editor 工具使用 `Torben.Calculator.Editor`, 測試使用 `Torben.Calculator.Tests`。
- `Torben.Samples` 系列組件區分純 C#、Runtime、Editor 與測試的編譯邊界, 不要求 namespace 同步分層。
- `[TorbenJuniorUtility]` 資源根目錄與 `TorbenJunior.sln` 專案名稱保留, 不作為 namespace 根。
- EditMode 測試驗證計算機邏輯與場景設定; `Scripts/Tests/PlayMode/CalculatorScenePlayModeTests.cs` 驗證場景在 PlayMode 載入後自動初始化及按鈕更新顯示。Unity 測試流程見 [Unity CLI 手冊](unity-cli-pipeline.md)。
