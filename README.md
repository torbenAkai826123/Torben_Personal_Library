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
| `Scripts/Runtime/`、`Scripts/Editor/` | 需要 Unity API 的執行期與 Editor 程式 |
| `Scripts/Samples/Calculator/` | 計算機範例, namespace 為 `Torben.Calculator`, 組件為 `Torben.Samples` 系列 |
| `Scripts/Samples/Calculator/Tests/`、`Scripts/Tests/PlayMode/` | 計算機的 EditMode 與 PlayMode 測試 |
| `dotnet/` | 共用 Assets 原始碼的 .NET 建置與測試設定, 見 [測試手冊](docs/dotnet-test.md) |
| `ExampleScene/` | 範例場景 |
| `TextAsset/` | CJK 字型與 TextMesh Pro SDF 資源 |
| `NamingRule.md` | C# 命名規則 |

## 授權

- 程式碼：[MIT License](LICENSE)
- 字型：SIL Open Font License 1.1，授權檔位於各字型資料夾內
