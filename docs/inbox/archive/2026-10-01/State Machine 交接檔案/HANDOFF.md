# Torben.StateMachine v1 — 交接摘要

## 本次交付

- `Torben.StateMachine.Unity6.5.zip`：Unity 匯入包，內含 `Torben.StateMachine/` 資料夾的 17 個 `.cs` 檔與 1 個 `.asmdef`；放入目標專案 `Assets/`。
- `Torben.StateMachine/`：可檢視及修改的 Core 原始碼，含本地驗證用 `.csproj`。
- `Torben.StateMachine.Tests/`：無第三方套件的行為測試。
- `Torben.StateMachine.Example/`：可執行的 Project 端使用範例。
- `ARCHITECTURE.md`：責任邊界、API、pipeline、fault semantics、設計決策與 Non-goals。
- `README.md`：匯入與本地執行說明。
- `FROZEN_IMPLEMENTATION_SPEC.md`：本次由使用者更新目標後提供的原始規格，作為後續核對依據。

## 已驗證

- Core 以 C# 9、`netstandard2.1` 建置成功；以警告視為錯誤建置時為 0 警告、0 錯誤。
- 42 個自動測試通過，涵蓋規格列出的 transition、request、Guard、completion、Stop、serial、fault、builder 不變條件，以及 C# 9 值型別的相等與解構。
- 基本範例成功執行，輸出 `door: Door opened`。
- Unity 匯入 ZIP 已檢查，只包含 Core `.cs` 與 `.asmdef`，沒有測試、範例、建置產物或第三方依賴。
- 匯入 ZIP 的 18 個檔案已逐一以 SHA-256 與目前交付的 Core 原始碼比對，內容一致。
- `.asmdef` 的獨立 assembly 與 No Engine References 設定已按 [Unity 6.5 官方 Assembly Definition 文件](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/compilation-and-code-reload/script-compilation/assembly-definition-files/class-assembly-definition-importer)核對；Unity Editor 實際匯入仍待下一次驗證。

## 經使用者核准的規格調整

Unity 6.5 官方文件列出的語言版本是 C# 9.0，而原 Frozen API 使用 C# 10 的 `readonly record struct` 和檔案層級 namespace。使用者已明確選擇「優先讓原始碼直接在 Unity 6.5 編譯；核准調整不相容的 Public API 語法」。因此四個公開 record struct 改為 C# 9 `readonly struct`，保留主要建構式、屬性、值相等、hash、運算子、解構及字串表示；所有 namespace 改為傳統區塊寫法。Core 的 lifecycle、pipeline、fault 和其他公開介面仍依 Frozen Spec。

這種調整不保留 C# record 自動產生的所有語法能力，例如 `with` 與 `init`；若後續專案程式直接依賴這些語法，需另外討論。此 Core 本身不使用它們。

## 下一次在 Codex 的工作

1. 取得目標 Unity 6.5 專案路徑，在可回復的工作副本中匯入 `Torben.StateMachine.Unity6.5.zip` 的 `Torben.StateMachine/` 資料夾至 `Assets/`。
2. 讓 Unity Editor 重新編譯，檢查 Console 無 Core 編譯錯誤或警告。若有差異，依實際 Unity 編譯訊息修正，再重跑本地測試。
3. 於 Unity 專案新增 Project 端 Host，從 `Start(context)`、`Request(request)`、`Execute()` 到 `Stop()` 做最小整合測試；確認 Guard Deny、正常轉換、LastResult 及一 Tick 不執行 Next State。
4. 視目標平台需要，做 Editor Play Mode 與實際 Player Build 驗證。尚未指定的平台不應宣稱已驗證。
5. 最後逐項對照 `FROZEN_IMPLEMENTATION_SPEC.md` 第 34 節 Definition of Done，將 Unity 實際編譯結果納入證據。

## 尚未驗證

本次環境沒有找到可使用的 Unity Editor 或目標 Unity 專案；因此「Unity Editor 內實際匯入／編譯」與任何平台 Player Build 均尚未執行。已完成的 C# 9／.NET Standard 2.1 建置是相容性強證據，但不能替代 Unity Editor 的實際結果。

## 參考

- [Unity 6.5 C# 編譯器與語言版本](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/environment-and-tools/overview-of-dot-net-in-unity/csharp-compiler)
- [Unity 6.5 .NET API 相容層級](https://docs.unity.com/en-us/engine/6000.5/manual/scripting/environment-and-tools/overview-of-dot-net-in-unity/dotnet-profile-support)
