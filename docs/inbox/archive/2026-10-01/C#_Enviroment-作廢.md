# 建立純 C# 測試環境

## 目標
讓 Core (不依賴 Unity 的程式) 可用 dotnet test 在 Unity 外部測試,
且 Unity 與 dotnet 共用同一份原始碼。

## 完成判準
1. 在倉庫根執行 `dotnet test "dotnet/TorbenJunior.sln"` 退出碼為 0, 至少一個範例測試通過。
2. Unity 開啟專案後編譯無錯誤; Core 的 asmdef 設定 noEngineReferences。
3. 反向驗證: 在 Core 暫時加入 `using UnityEngine;`, Unity 與 dotnet 兩邊都編譯失敗; 驗證後還原。
4. `docs/dotnet-test.md` 記錄 SDK 版本與執行指令。

## 範圍與限制
- 可新增: `Assets/[TorbenJuniorUtility]/Scripts/` 下的 Core / Runtime / Editor / Tests 資料夾與 asmdef、倉庫根 `dotnet/`、`docs/dotnet-test.md`、`.gitignore` 的 bin/obj 規則。
- 不改 `Packages/`。
- 不安裝任何工具; .NET SDK 已由使用者安裝。
- 路徑一律加引號。

## 參考結構
倉庫根/
├─ Assets/[TorbenJuniorUtility]/Scripts/
│  ├─ Core/          TorbenJunior.Core.asmdef (noEngineReferences)
│  ├─ Runtime/       TorbenJunior.Runtime.asmdef → 引用 Core
│  ├─ Editor/        TorbenJunior.Editor.asmdef (僅 Editor 平台)
│  └─ Tests/
│     ├─ PlayMode/   asmdef
│     └─ EditMode/   asmdef
├─ dotnet/
│  ├─ TorbenJunior.Core/        .csproj (Compile Include 指向 Assets 內的 Core)
│  ├─ TorbenJunior.Core.Tests/  NUnit 測試專案
│  └─ TorbenJunior.sln
└─ docs/dotnet-test.md
