# Torben Personal Library

個人用的 Unity 工具庫 (My personal Unity utility library) 。

## 環境

- Unity 6000.5.5f1 (URP)
- 字型檔使用 Git LFS，clone 前請先安裝 [Git LFS](https://git-lfs.com/)

## 內容

主要內容位於 `Assets/[TorbenJuniorUtility]/`：

| 路徑 | 說明 |
|---|---|
| `Scripts/Core/` | 純 C# 函式庫, namespace 根為 `Torben` |
| `Scripts/Core/StateMachine/` | `Torben.StateMachine` 固定 Flow 同步狀態機, 由 `Torben.Core` 編譯 |
| `Scripts/Runtime/`、`Scripts/Editor/` | 需要 Unity API 的執行期與 Editor 程式 |
| `Scripts/Samples/Calculator/` | 計算機範例, namespace 為 `Torben.Calculator`, 組件為 `Torben.Samples` 系列 |
| `Scripts/Samples/StateMachine/` | 門鎖流程與 `DoorStateMachineHost` 示範, 沿用 `Torben.Samples` 系列組件 |
| `Scripts/Tests/PlayMode/` | 函式庫 Host 的 PlayMode 測試, 組件為 `Library.Tests.PlayMode` |
| `Scripts/Samples/Calculator/Tests/EditMode/`、`.../Tests/PlayMode/` | 計算機測試, 分別由 `Samples.Tests.EditMode` 與 `Samples.Tests.PlayMode` 編譯 |
| `Scripts/Samples/StateMachine/Tests/PlayMode/` | 門鎖 Host 示範測試, 透過 asmref 加入 `Samples.Tests.PlayMode` |
| `dotnet/` | 共用 Assets 原始碼的 .NET 建置與測試設定, 見 [測試手冊](docs/dotnet-test.md) |
| `ExampleScene/` | 範例場景 |
| `TextAsset/` | CJK 字型與 TextMesh Pro SDF 資源 |
| `NamingRule.md` | C# 命名規則 |

## 狀態機使用

完整的責任邊界、API、純 C# 範例、Unity Host lifecycle、故障語意與 Non-goals 見 [狀態機使用與 API 手冊](docs/state-machine.md)。

純 C# 程式使用 `Torben.StateMachine` 的 `StateFlowBuilder` 建置固定 Flow, 再建立 `StateMachine<TContext, TOutput>`。由 Project 呼叫 `Start(context)`、`Request(request)`、`Execute()` 與 `Stop()`; `RequestResult` 只表示 Pending slot 的接受結果, 轉換在下一次 `Execute()` 處理。

Unity 的 `StateMachineHost<TContext, TOutput>` 位於 `Scripts/Runtime/StateMachine/`, 由 `Torben.Runtime` 編譯。具體子類提供 `CreateMachine()` 與 `CreateInitialContext()`; Host 啟用時 Start, Running 時每個 Update 執行一個 Tick, 停用時 Stop。重新啟用會在同一機器 Start 新 run, 清除 LastResult 並保留 Serial counter。Faulted 後停止自動 Tick, 原例外交給 Unity 記錄; Host 不自動重試。

可將 `DoorStateMachineHost` 加到 GameObject, 用 `RequestOpen(false)` 驗證 Guard Deny, 再用 `RequestOpen(true)` 在後續 Update 開門。結果由 `Machine.LastResult` 讀取, 示範輸出為 `Door opened`。每個 Host 建立自己的 State 實例。

## 授權

- 程式碼：[MIT License](LICENSE)
- 字型：SIL Open Font License 1.1，授權檔位於各字型資料夾內
