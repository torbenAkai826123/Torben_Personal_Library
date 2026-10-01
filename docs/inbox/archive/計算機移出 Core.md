# 計算機移出 Core, 改為範例 (取代前一版)

## 目標
計算機不再屬於 `Torben.Core` 組件, 改為獨立的範例組件; 範例與函式庫本體以組件區分, namespace 統一使用 `Torben` 根。計算機的測試保留作為工具鏈的冒煙測試。

## 完成判準
1. `CalculatorModel` 位於 `Scripts/Samples/Core/`, 組件為 `Torben.Samples` (noEngineReferences)。
2. `CalculatorView` 位於 `Scripts/Samples/Runtime/`, 組件為 `Torben.Samples.Runtime`, 引用 `Torben.Samples`。
3. `Torben.Core` 不含任何 Calculator 型別; Core 沒有原始碼時, Unity 與 dotnet 兩邊仍能正常編譯。
4. `unity test` 執行 EditMode 全部通過, 回報測試數量。
5. `dotnet test "dotnet/TorbenJunior.sln"` 退出碼為 0, 且實際執行了 `CalculatorModel` 的測試 (回報測試數量); dotnet 端的專案配置由 Code 決定並說明。
6. `NamingRule.md`: 刪除 `TorbenJuniorUtility` namespace 根, 補上組件命名規則 (函式庫: `Torben.Core` / `Torben.Runtime` / `Torben.Editor`; 範例: `Torben.Samples` / `Torben.Samples.Runtime`)。
7. `Assets/` 與 `dotnet/` 底下沒有任何 `namespace TorbenJuniorUtility` (含舊的 `TorbenJuniorUtility.Tests`), 以搜尋結果佐證。

## 範圍與限制
- 計算機 namespace 維持 `Torben.Calculator`。
- 不抽取輸入緩衝, 不修改計算機邏輯。
- 搬移時保留 `.meta` 的 GUID。
- 資料夾名稱 `[TorbenJuniorUtility]` 與 `TorbenJunior.sln` 不變 (專案名稱)。