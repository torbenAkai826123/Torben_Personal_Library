# Goal driven 請求範例

以下是格式範例，不是待執行請求。實際請求請另存至 `docs/inbox/`。

## 目標

在 Unity 編輯器修改 `Hello` 元件的 `number` 後，Console 只在數值變更時輸出一次新值。

## 完成判準

1. 在掛有 `Hello` 的物件上變更 `number`，每次變更恰好新增一筆含新值的訊息。
2. 數值未變更時，連續執行數個畫格不產生額外訊息。
3. 可用 Unity Play Mode 實測；若本機無法啟動 Unity，須明確回報未驗證項目。

## 範圍與限制

- 允許修改 `Assets/[TorbenJuniorUtility]/Hello.cs` 及達成本目標必需的同專案資源。
- 不更動 `Packages/`、`ProjectSettings/` 或其他元件的行為。

## 建議做法（選填）

可先檢查現有數值比較與輸出時機。若建議做法無法達到完成判準，可在上述範圍內改採可驗證的做法，並在交付時說明。
