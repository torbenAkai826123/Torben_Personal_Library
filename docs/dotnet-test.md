# 純 C# 測試 (dotnet test)

Core (不依賴 Unity 的程式) 可以在 Unity 外部用 `dotnet test` 測試。Unity 與 dotnet 編譯的是同一份原始碼：`Assets/[TorbenJuniorUtility]/Scripts/Core/`。

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
└─ Tests/
   ├─ EditMode/  Torben.Tests.EditMode.asmdef
   └─ PlayMode/  Torben.Tests.PlayMode.asmdef
dotnet/
├─ Torben.Core/        Torben.Core.csproj (Compile Include 指向 Assets 內的 Core)
├─ Torben.Core.Tests/  NUnit 測試
└─ TorbenJunior.sln
```

- `Torben.Core.csproj` 以 `**/*.cs` 收進 Core 資料夾所有檔案；在 Core 新增 `.cs` 不需改 csproj。
- dotnet 測試放在 `dotnet/Torben.Core.Tests/`，不放進 `Assets/`，Unity 不會編譯它們。
- `bin/`、`obj/` 已列入 `.gitignore`；`dotnet/` 底下的 `.csproj`、`.sln` 例外保留在版控中。

## Core 的限制

- 兩邊都不允許引用 `UnityEngine`／`UnityEditor`：Unity 由 asmdef 的 `noEngineReferences` 擋下，dotnet 則因沒有 Unity 組件而無法編譯。在 Core 加入 `using UnityEngine;` 時，兩邊都會出現 `CS0246` (2026-10-01 驗證)。
- 只能用 .NET Standard 2.1 與 C# 9 的功能；dotnet 端已用相同設定，超出的寫法會在 `dotnet test` 時就編譯失敗。
- dotnet 測試只驗證邏輯正確性；GC 與效能數字以 Unity Performance Testing 為準。
