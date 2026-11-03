# Claude 專屬偏好

@AGENTS.md

共用規則以上方引入的 `AGENTS.md` 為準；本檔只補充 Claude 的個人偏好。

## 語言

- 一律使用繁體中文回覆；程式碼、指令與專有名詞保留原文。
- 程式碼註解用中文，依 `AGENTS.md` 書寫慣例 (半形標點 + 空格)。
- Commit 訊息用英文，沿用既有的 Conventional Commits 格式 (`feat:`、`fix:`、`chore:`、`docs:`、`refactor:`)。

## 對話標題

- 處理 `docs/inbox/` 的請求時，讀完請求檔、確定主題後，用 `set_session_title` 把對話標題改成請求主題的簡短描述 (例如「Log 視窗加篩選按鈕」)，不要沿用「整理 Inbox」這類指令名稱。
- 同一對話處理多份請求時，標題以主要或第一份請求為準；主題明顯改變時再更新。

## Git

- 只在使用者要求時才 commit 或 push。
- 只 commit 本次任務相關的檔案；工作區中原有或不明的變更不要順手加入，先回報。
