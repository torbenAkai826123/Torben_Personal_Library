# Torben.StateMachine v1

純 C#、固定 Flow 的同步狀態機 Core。所有公開型別均在 `Torben.StateMachine` 命名空間；Core 不引用 Unity 或遊戲專案型別。Core 以 Unity 6.5 所支援的 C# 9 語法及 .NET Standard 2.1 API 編譯。

## 交付內容

- `Torben.StateMachine/`：Core 原始碼與 `Torben.StateMachine.asmdef`。將整個資料夾複製到 Unity 專案的 `Assets/` 下；`.csproj` 僅供本地編譯驗證，匯入時可略過。
- `Torben.StateMachine.Tests/`：無第三方測試套件的自動測試程式。
- `Torben.StateMachine.Example/`：可執行的基本用法，演示 Project-owned Context、Output、State、Guard 及由 Host 驅動 `Execute()`。
- `ARCHITECTURE.md`：API、執行順序、故障語意與設計決策。

## 編譯條件

原 Frozen API 的 `readonly record struct` 與檔案層級 namespace 屬 C# 10；Unity 6.5 [官方編譯器文件](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/environment-and-tools/overview-of-dot-net-in-unity/csharp-compiler)明列 C# 9.0。使用者已核准以 Unity 6.5 原始碼相容性為優先：四個公開 record struct（`StateId`、`TransitionId`、`StateResult<TOutput>`、`TransitionRequest<TContext>`）改為具相同主要資料欄位、建構式與值相等語意的 `readonly struct`；namespace 改用傳統區塊寫法。這是刻意的 Public API 語法差異。Core 專案以 C# 9 和 `netstandard2.1` 建置；Unity 6.5 [官方文件](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/environment-and-tools/overview-of-dot-net-in-unity/dotnet-profile-support)列 .NET Standard 2.1 為預設 API 相容層級。

測試與範例執行專案採 `net10.0`，因執行環境只安裝 .NET 10 SDK；Core 本身是 `netstandard2.1`，並以 `LangVersion` 9.0 編譯。

## 執行測試及範例

```powershell
dotnet run --project Torben.StateMachine.Tests/Torben.StateMachine.Tests.csproj
dotnet run --project Torben.StateMachine.Example/Torben.StateMachine.Example.csproj
```

測試及範例是獨立的 Project 端程式，不放入 Core。此交付已在獨立 .NET 編譯與執行測試；尚未在使用者的 Unity Editor／目標平台中實際匯入與編譯，因此 Unity 內的最終驗證仍需在目標專案執行。
