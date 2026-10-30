# 引用測試區說明

兩個都不是。dotnet 建置出來的 DLL 不會被 Unity 引用，Common 也只是你自己的練習區。「不用搬來搬去」是因為 Unity 和 dotnet 讀的是同一份原始碼，不是共用 DLL。

「不用搬」是怎麼做到的

dotnet/Torben.Core/Torben.Core.csproj 本身沒有程式碼，它是直接去 Assets 裡抓 .cs 來編譯：

<Compile Include="../../Assets/[TorbenJuniorUtility]/Scripts/Core/**/*.cs" />

所以同一份檔案會被編譯兩次：

| 誰在編譯 |                     依據                     |          用途           |
| --- | --- | --- |
| Unity   |Scripts/Core/Torben.Core.asmdef             | 遊戲裡實際使用         |
| 	 |          (noEngineReferences: true)                     |
|  dotnet    | dotnet/Torben.Core/Torben.Core.csproj        | 不開 Unity 也能跑     |
 |          |                                      | dotnet test 驗證邏輯    |

dotnet 產生的 bin/、obj/ 只給 dotnet 測試用，Unity 看不到也不需要。

dotnet/Common/ 在 Assets/ 外面，Unity 完全不會讀它。它現在是一個獨立的 console 程式，適合用來練習或臨時試寫法，跟函式庫無關。

## 寫純 C# 內容的流程

以現有的 StateMachine 為例，新功能這樣做：

1. 原始碼放在 Assets/[TorbenJuniorUtility]/Scripts/Core/<功能名>/，例如 Scripts/Core/Timer/Timer.cs。
   - namespace 用 Torben.<功能名>，例如 Torben.Timer。
   - 不能 using UnityEngine;，否則 Unity 端會因為 noEngineReferences 而編譯失敗。
   - 語法限制在 .NET Standard 2.1 + C# 9。dotnet 端也設成一樣，寫了太新的語法會在 dotnet test 時直接報錯。
   - 新檔案不用改 csproj，**/*.cs 會自動把它納入。
2. 測試寫在 dotnet/Library.Tests/，用 NUnit，寫法可參考 StateMachineContractTests.cs。
3. 在倉庫根目錄執行測試：
dotnet test "dotnet/TorbenJunior.sln"
4. 回到 Unity：切回 Editor 後會自動編譯同一份檔案，並幫新的 .cs 產生 .meta。commit 時 .cs 和 .meta 要一起提交。

範例性質的程式碼（不是函式庫本體）則放在 Scripts/Samples/<名稱>/，測試寫在 dotnet/Samples.Tests/，機制一樣。

### 讓 Common 呼叫 Core

想在 Common 裡用 console 快速試 Core 的 API，可以在 Common.csproj 加回這段：

<ItemGroup>
    <ProjectReference Include="../Torben.Core/Torben.Core.csproj" />
</ItemGroup>

不過正式的驗證還是建議寫成 Library.Tests 裡的測試，這樣之後每次 dotnet test 都會自動重跑。

以上都是檢查現有檔案得到的結果，這次沒有改動任何檔案。
