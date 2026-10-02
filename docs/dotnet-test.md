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
├─ Core/       Torben.Core.asmdef (noEngineReferences), 含 StateMachine/ 純邏輯
├─ Runtime/    Torben.Runtime.asmdef → Core, 含 StateMachine/ Unity Host
├─ Editor/     Torben.Editor.asmdef (僅 Editor)
├─ Tests/
│  └─ PlayMode/  Library.Tests.PlayMode.asmdef
└─ Samples/
   ├─ Calculator/  Torben.Samples.asmdef (noEngineReferences) → Core
   │  ├─ Runtime/  Torben.Samples.Runtime.asmdef → Samples/Core/Runtime
   │  ├─ Editor/   Torben.Samples.Editor.asmdef
   │  └─ Tests/
   │     ├─ EditMode/  Samples.Tests.EditMode.asmdef
   │     └─ PlayMode/  Samples.Tests.PlayMode.asmdef
   └─ StateMachine/  asmref 指向 Torben.Samples
      ├─ Runtime/  asmref 指向 Torben.Samples.Runtime
      └─ Tests/PlayMode/  asmref 指向 Samples.Tests.PlayMode
dotnet/
├─ Torben.Core/                       Compile Include 指向 Assets 內的 Core
├─ Library.Tests/                     狀態機 50 項 NUnit 契約測試
├─ Torben.Samples/                    Compile Include 指向 Samples, 排除 Runtime/Editor/Tests 子資料夾, 引用 Core
├─ Samples.Tests/                     計算機 6 項與門鎖示範 2 項 NUnit 測試
└─ TorbenJunior.sln
```

- `Torben.Core.csproj` 以 `**/*.cs` 收進 Core 資料夾所有檔案；在 Core 新增 `.cs` 不需改 csproj。
- `Torben.Samples.csproj` 收進 `Samples/` 下所有檔案，但排除 `Runtime/`、`Editor/`、`Tests/` 子資料夾 (那些屬於需要 Unity 的 asmdef)。新增範例時沿用相同的資料夾分法即可。
- dotnet 測試放在 `dotnet/` 底下，不放進 `Assets/`，Unity 不會編譯它們。
- `Library.Tests/StateMachineContractTests.cs` 將交接包的 42 項 Console 行為案例接為 NUnit 測試, 保留案例名稱與原有判準, 另加 8 項完成結果擷取 (Exit completion、callback 例外、同實例重新進入) 的案例; `dotnet test` 應實際探索並執行全部 50 項。
- `bin/`、`obj/` 已列入 `.gitignore`；`dotnet/` 底下的 `.csproj`、`.sln` 例外保留在版控中。

## Core 與 Samples 的限制

- 兩邊都不允許引用 `UnityEngine`／`UnityEditor`：Unity 由 asmdef 的 `noEngineReferences` 擋下，dotnet 則因沒有 Unity 組件而無法編譯。在 Core 加入 `using UnityEngine;` 時，兩邊都會出現 `CS0246` (2026-10-01 驗證)。
- 只能用 .NET Standard 2.1 與 C# 9 的功能；dotnet 端已用相同設定，超出的寫法會在 `dotnet test` 時就編譯失敗。
- dotnet 測試只驗證邏輯正確性；GC 與效能數字以 Unity Performance Testing 為準。

## 命名與 Unity 驗證

- 自有 namespace 與 production 組件以 `Torben` 為根; 測試組件依測試對象及模式命名, 不強制 `Torben` 字首。計算機 Model 與 View 使用 `Torben.Calculator`, Editor 工具使用 `Torben.Calculator.Editor`, 測試使用 `Torben.Calculator.Tests`。
- `Torben.Samples` 系列組件區分純 C#、Runtime 與 Editor; Unity 範例測試分別編入 `Samples.Tests.EditMode`、`Samples.Tests.PlayMode`, 不要求 namespace 同步分層。純 .NET 測試由 `Library.Tests`、`Samples.Tests` 編譯。
- `[TorbenJuniorUtility]` 資源根目錄與 `TorbenJunior.sln` 專案名稱保留, 不作為 namespace 根。
- `Scripts/Samples/Calculator/Tests/EditMode/CalculatorTests.cs` 驗證計算機邏輯與場景設定; `Scripts/Samples/Calculator/Tests/PlayMode/CalculatorScenePlayModeTests.cs` 驗證場景在 PlayMode 載入後自動初始化及按鈕更新顯示。Unity 測試流程見 [Unity CLI 手冊](unity-cli-pipeline.md)。
- `Scripts/Tests/PlayMode/StateMachineHostTests.cs` 驗證真實 Unity lifecycle 的啟動、Guard Deny、Tick 順序、LastResult、停用/重新啟用與 Fault 停止自動 Tick; `Scripts/Samples/StateMachine/Tests/PlayMode/DoorStateMachineHostTests.cs` 驗證門鎖示範。純邏輯的 `Runtime/` 子資料夾仍位於 Core; 名稱不代表 Unity API 依賴。
- 接收版本保留 C# 9 `readonly struct` 與區塊 namespace 調整; 沒有匯入交接包的獨立 `Torben.StateMachine.asmdef`。Unity 與 .NET 都將原始碼編入 `Torben.Core`, namespace 維持 `Torben.StateMachine`。
- .NET 通過不代表 Unity GC 或 IL2CPP Player 已驗證; Core XML 註解標示配置行為, Guards 列舉及 Project lifecycle 的配置需以 Unity 量測。
