# 純 C# 開發流程 (入門)

> 本文是給人看的入門說明；規則以 [`AGENTS.md`](../../AGENTS.md) 與 [dotnet 測試手冊](../dotnet-test.md) 為準。

一句話：**程式寫在 `Assets/` 裡，測試寫在 `dotnet/` 裡**。不需要另外架環境，也不用在兩邊之間搬檔案。

版本、目錄結構與限制的完整說明見 [dotnet 測試手冊](../dotnet-test.md)；本文只講日常怎麼做。

## 核心觀念：一份原始碼，兩邊編譯

`dotnet/Torben.Core/Torben.Core.csproj` 本身沒有程式碼，它用萬用字元去抓 Assets 裡的檔案：

```xml
<Compile Include="../../Assets/[TorbenJuniorUtility]/Scripts/Core/**/*.cs" />
```

所以同一個 `.cs` 會被編譯兩次：

| 編譯者 | 依據 | 用途 |
|---|---|---|
| Unity | `Scripts/Core/Torben.Core.asmdef` (`noEngineReferences: true`) | 遊戲實際使用 |
| dotnet | `dotnet/Torben.Core/Torben.Core.csproj` | 不開 Unity 也能用 `dotnet test` 驗證邏輯 |

- dotnet 建置出的 DLL (`dotnet/**/bin/`) **不會**被 Unity 引用，只給 dotnet 測試使用，且已被 git 忽略。
- 「不用搬」指的是原始碼只有一份：在任一邊改 `Scripts/Core/` 底下的檔案，另一邊下次編譯就會用到。
- `dotnet/` 在 `Assets/` 外面，Unity 不會讀取裡面的測試與練習專案。

## 用哪個方案檔開啟

兩個方案檔編輯的是同一批檔案，差別在包含的範圍：

| 方案檔 | 怎麼開 | 包含 | 適合 |
|---|---|---|---|
| `dotnet/TorbenJunior.sln` | 用 Visual Studio／Rider 直接開 | 只有純 C# 專案 (Core、Samples 純邏輯部分、NUnit 測試、練習區) | 寫 Core 與跑 dotnet 測試 |
| `TorbenJnr Utility.slnx` (倉庫根) | Unity 選單 Assets > Open C# Project，由 Unity 自動產生 | 所有 Unity 組件 (含 Runtime、Editor) | 寫需要 Unity API 的程式 |

- 寫純 C# 時建議開 `dotnet/TorbenJunior.sln`：用不到的 Unity 組件不會出現在專案裡，也能直接在測試總管 (Test Explorer) 執行測試。
- Unity Editor 同時開著也沒關係。切回 Editor 時，Unity 會偵測到檔案變更並重新編譯。

## 新增一個純 C# 功能

以下用一個假想的冷卻計時器 `Cooldown` 示範，專案內目前沒有這個功能。

### 1. 在 Core 建立原始碼

路徑：`Assets/[TorbenJuniorUtility]/Scripts/Core/Timers/Cooldown.cs`

```csharp
namespace Torben.Timers
{
    /// <summary>
    /// 冷卻計時器. 由呼叫端每幀傳入經過時間.
    /// </summary>
    public sealed class Cooldown
    {
        private readonly float duration;
        private float remaining;

        /// <summary>建立冷卻計時器. 僅初始化時配置.</summary>
        public Cooldown(float duration)
        {
            this.duration = duration;
        }

        /// <summary>冷卻是否已結束. 零配置.</summary>
        public bool IsReady => remaining <= 0f;

        /// <summary>開始冷卻. 零配置.</summary>
        public void Trigger()
        {
            remaining = duration;
        }

        /// <summary>推進時間. 每幀呼叫, 零配置.</summary>
        public void Tick(float deltaTime)
        {
            if (remaining > 0f)
            {
                remaining -= deltaTime;
            }
        }
    }
}
```

要點：

- 一個功能一個子資料夾，namespace 用 `Torben.<功能名>`。
- 不需要改 csproj，`**/*.cs` 會自動收進新檔案。
- 不能 `using UnityEngine;`。需要 `Vector3`、`Time.deltaTime` 這類東西時，改由參數傳入 (上例的 `deltaTime`)，或把那一段放到 Runtime。
- 只能用 .NET Standard 2.1 與 C# 9 的語法。
- 公開 API 的 XML 註解要寫明配置行為：零配置／僅初始化時配置／每次呼叫配置。

### 2. 在 dotnet 撰寫測試

路徑：`dotnet/Library.Tests/CooldownTests.cs`。`Library.Tests` 已引用 `Torben.Core`，不需要額外設定。

```csharp
using NUnit.Framework;
using Torben.Timers;

namespace Torben.Timers.Tests
{
    public sealed class CooldownTests
    {
        [Test]
        public void Trigger_ThenTickFullDuration_IsReady()
        {
            var cooldown = new Cooldown(1f);

            cooldown.Trigger();
            Assert.That(cooldown.IsReady, Is.False);

            cooldown.Tick(1f);
            Assert.That(cooldown.IsReady, Is.True);
        }
    }
}
```

NUnit 版本是 3.x，`Assert.That` 的寫法與 Unity Test Framework 相同。

### 3. 執行測試

在倉庫根目錄執行 (路徑含空白與 `[` `]`，一律加引號)：

```bash
dotnet test "dotnet/TorbenJunior.sln"
```

最後出現 `已通過!` 且 `失敗: 0` 就代表通過。只想跑剛寫的測試時：

```bash
dotnet test "dotnet/Library.Tests/Library.Tests.csproj" --filter "FullyQualifiedName~Cooldown"
```

在 Visual Studio 也可以從「測試 > 測試總管」執行。

### 4. 回到 Unity

切回 Unity Editor，讓它重新編譯並為新的 `.cs` 與資料夾產生 `.meta`。提交時 `.cs` 和對應的 `.meta` 要一起提交。

範例性質的程式碼 (不是函式庫本體) 改放 `Scripts/Samples/<名稱>/`，測試寫在 `dotnet/Samples.Tests/`，流程相同。

## 練習區：console 專案

想用 `Console.WriteLine` 快速試寫法、不打算留下測試時，可以在 `dotnet/` 底下建立 console 專案 (例如 `dotnet/Common/`)。它不在 `Assets/` 裡，不會影響 Unity。

```bash
dotnet new console -n Common -o "dotnet/Common"
dotnet sln "dotnet/TorbenJunior.sln" add "dotnet/Common/Common.csproj"
dotnet run --project "dotnet/Common"
```

建立後把 csproj 裡的 `<LangVersion>` 設成 `9.0`，才能和 Core 用同一個 C# 版本試寫法。想在練習區呼叫 Core 的 API，就加入：

```xml
<ItemGroup>
    <ProjectReference Include="../Torben.Core/Torben.Core.csproj" />
</ItemGroup>
```

注意：

- 不要直接複製測試專案的 csproj 來改。測試套件 `Microsoft.NET.Test.Sdk` 會自動產生 `Main`，和你自己寫的 `Main` 衝突。
- `<AssemblyName>` 不可和其他專案重複，否則建置時輸出的 DLL 會互相覆蓋。
- 練習區的程式碼不會進入 Unity；確定要留下的邏輯請移到 `Scripts/Core/`，並補上測試。

## 常見錯誤

| 現象 | 原因 | 處理 |
|---|---|---|
| `CS0246` 找不到 `UnityEngine` | Core 引用了 Unity API | 改由參數傳入，或把該段移到 Runtime |
| `CS8773` 在 C# 9.0 中無法使用 'xxx' 功能 | 用了 C# 10 以上的語法 (例如檔案範圍 namespace `namespace X;`) | 改用 C# 9 寫法 (例如區塊 namespace `namespace X { }`) |
| `dotnet run` 顯示 Unable to run your project | 專案不是可執行檔 | csproj 加上 `<OutputType>Exe</OutputType>` |
| `CS0017` 定義了一個以上的進入點 | console 專案裝了 `Microsoft.NET.Test.Sdk` | 移除測試套件與 `IsTestProject` |
| 新寫的測試沒有出現在結果裡 | 測試檔不在 `dotnet/` 的測試專案，或方法少了 `[Test]` | 確認檔案位置與屬性 |
| 在 IDE 改完後，Unity 裡還是舊行為 | Unity 尚未重新編譯 | 切回 Editor 或執行 Assets > Refresh |

## dotnet 測試不能代替的事

- dotnet 測試只驗證邏輯是否正確。GC 配置、效能，以及 Android IL2CPP 上的行為，仍要在 Unity 裡量測與驗證。
- 和 Unity lifecycle 有關的行為 (例如 `MonoBehaviour` 的啟用與停用) 要寫成 Unity 的 PlayMode 測試，流程見 [Unity CLI 手冊](../unity-cli-pipeline.md)。
