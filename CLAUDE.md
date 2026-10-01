# Claude 專屬偏好

@AGENTS.md

共用規則以上方引入的 `AGENTS.md` 為準；本檔只補充 Claude 的個人偏好。

## 語言

- 一律使用繁體中文回覆；程式碼、指令與專有名詞保留原文。
- 程式碼註解用中文，依 `AGENTS.md` 書寫慣例 (半形標點 + 空格)。
- Commit 訊息用英文，沿用既有的 Conventional Commits 格式 (`feat:`、`fix:`、`chore:`、`docs:`、`refactor:`)。

## Git

- 只在使用者要求時才 commit 或 push。
- 只 commit 本次任務相關的檔案；工作區中原有或不明的變更不要順手加入，先回報。

## 測試

- 改到 C# 程式後，若 Unity Editor 未開啟此專案，依 `docs/unity-batchmode.md` 用 batch mode 跑 EditMode 測試並回報結果。
- Editor 開著時不跑 batch mode，改為提醒使用者在 Test Runner 手動執行。
