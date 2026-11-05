# Event 系統 v1 設計與實作需求

- 對象: 負責實作的 AI。前提是讀者已讀過:
  - `event-system-context-report.md` (Event 系統設計前置報告: StateMachine 與現有溝通方式)
  - `statemachine-design-philosophy.md` (StateMachine 開發偏好與設計原則)
  - `docs/log-system.md` 中 sink 例外隔離的部分 (本系統沿用同一套決定)
- 目的: 提供「明確」的事件機制, 用於 **通知事實** (一件事發生, 多個對象跟著反應)。不用於流程控制。
- 使用者背景: 使用者沒有以事件為主要開發方式的經驗。**教學文件 (第 7 節) 是本需求的主要交付物之一, 與程式同等重要。**

---

## 1. 設計原則 (實作時不得違反)

| 原則 | 說明 | 來源 |
|---|---|---|
| 只通知事實 | 事件描述「發生了什麼」, 不要求系統改變流程。要求轉換用 `TransitionRequest`, 回報結果用 `Output` | 設計原則文件 §7 |
| 沒有全域入口 | **不提供** 單例或靜態事件匯流排。每種事件一個頻道實例, 由組裝的地方 (Composition Root) 建立並傳給需要的物件 | 設計原則文件 §3、§5 |
| 依權限傳遞 | 發布者只拿到「可發布」的介面, 訂閱者只拿到「可訂閱」的介面 | 讓依賴關係出現在型別上 |
| 訂閱必有結束點 | 訂閱一定回傳 handle; Unity 物件在訂閱的同一行綁定生命週期 | 前置報告 §2.2、§5 |
| 一個接收者失敗不影響其他接收者 | 每個訂閱者各自 `try / catch`, 例外不傳回發布端, 每個訂閱只回報一次 | 沿用 Log sink 的決定 |
| 不改 StateMachine Core | StateMachine v1 刻意不提供通知, Event 系統不得在 Core 加入任何事件或回呼 | 前置報告 §1.4 |
| 每幀零配置 | `Publish` 不得配置記憶體; 訂閱 / 取消訂閱屬於一次性操作, 可以配置 | AGENTS.md GC 規則 |

---

## 2. 位置與命名

| 項目 | 規格 |
|---|---|
| namespace | `Torben.Events` (避免與 `UnityEngine.EventSystems` 混淆) |
| Core | `Scripts/Core/Events/` (組件 `Torben.Core`) |
| Runtime | `Scripts/Runtime/Events/` (組件 `Torben.Runtime`) |
| 範例 | `Scripts/Samples/Events/` |
| 測試 | dotnet `Library.Tests` (Core); PlayMode (Runtime 與範例) |

資料夾命名依專案現有慣例調整 (例如 Log 用 `Logs/`), 若有差異在回報中說明。

---

## 3. Core API

### 3.1 型別

```csharp
namespace Torben.Events
{
    /// 訂閱者的回呼。以 in 傳遞, 事件 struct 不複製。
    public delegate void EventCallback<T>(in T e) where T : struct;

    /// 可發布的一面: 交給發布者。
    public interface IEventPublisher<T> where T : struct
    {
        void Publish(in T e);
    }

    /// 可訂閱的一面: 交給訂閱者。
    public interface IEventSource<T> where T : struct
    {
        EventSubscription Subscribe(EventCallback<T> callback);
        int SubscriberCount { get; }
    }

    /// 一種事件的頻道。由 Composition Root 建立。
    public sealed class EventChannel<T> : IEventPublisher<T>, IEventSource<T> where T : struct
    {
        public EventChannel(string debugName = null);   // null 時使用 typeof(T).Name
        public string DebugName { get; }
        public bool IsTraceEnabled { get; set; }         // 預設 false, 見 3.6
        public int SubscriberCount { get; }
        public void Publish(in T e);
        public EventSubscription Subscribe(EventCallback<T> callback);
    }

    /// 訂閱的 handle。Dispose 即取消訂閱; 重複 Dispose 不做事。
    public sealed class EventSubscription : System.IDisposable
    {
        public bool IsActive { get; }
        public void Dispose();
    }

    /// 一次取消多個訂閱。用於非 Unity 物件或 Composition Root。
    public sealed class EventSubscriptionGroup : System.IDisposable
    {
        public void Add(EventSubscription subscription);  // null 丟 ArgumentNullException; 已 Dispose 的群組加入時立即取消該訂閱
        public int Count { get; }
        public void Dispose();                            // 取消全部並清空; 之後仍可再 Add (立即取消)
    }

    /// 訂閱者例外的回報入口 (比照 Log.SinkErrorHandler)。
    public static class EventErrors
    {
        public static System.Action<string, System.Exception> Handler;   // (頻道 DebugName, 例外); null 時不回報
    }
}
```

- 事件型別限制為 `struct`: 不配置、不裝箱; 教學文件建議寫成 `readonly struct`。
- 名稱可依專案慣例調整, 但「發布 / 訂閱分成兩個介面」與「訂閱回傳 handle」不可省略。

### 3.2 Publish 的行為

1. 取得目前訂閱者陣列的快照 (copy-on-write, 只讀一次參考)。
2. 依 **訂閱順序** 逐一呼叫。文件需說明「不應依賴順序」。
3. 呼叫前檢查該訂閱是否仍有效 (見 3.3), 已取消的略過。
4. 每個呼叫各自 `try / catch`; 例外交給 `EventErrors.Handler`, **每個訂閱只回報第一次**; 例外不傳回發布端。`Handler` 自己拋的例外捕捉並忽略。
5. 沒有訂閱者時直接返回。
6. 不配置記憶體。

### 3.3 訂閱與取消

| 情況 | 規格 |
|---|---|
| `Subscribe(null)` | 丟 `ArgumentNullException` |
| 同一個 callback 訂閱兩次 | 允許, 視為兩個獨立訂閱 (各自的 handle), 會被呼叫兩次 |
| 在 Publish 進行中訂閱 | 新訂閱 **不會** 在這一次 Publish 中被呼叫 (快照語意) |
| 在 Publish 進行中取消 (自己或其他訂閱) | 被取消的訂閱若還沒輪到, **不會** 再被呼叫。實作: 訂閱帶有效旗標, 取消時先設為無效, 再從陣列移除 |
| 重複 Dispose | 不做事 |
| 訂閱 / 取消的配置 | 允許 (陣列複製與 handle 物件)。XML 註解標明「每次呼叫配置」 |

### 3.4 巢狀發布

- 訂閱者在回呼中發布其他事件 (或同一事件): **允許**, 同步執行。
- 以 `[ThreadStatic]` 計數目前執行緒的巢狀深度。超過上限 (常數 32, 與文件共用一處定義) 時, **不執行** 該次 Publish, 並以 `EventErrors.Handler` 回報 (例外型別自訂或用 `InvalidOperationException`, 訊息包含頻道名稱與「可能是事件互相觸發的無限循環」)。同一頻道的深度錯誤也只回報一次。

### 3.5 執行緒

- v1 預設在 **主執行緒** 使用。訂閱 / 取消以 `lock` 保護, 可從任何執行緒呼叫; `Publish` 會在呼叫它的執行緒上執行回呼, 不做執行緒切換。
- Core 無法判斷主執行緒, 不做檢查; 在文件中明確說明「在背景執行緒發布時, 回呼也在背景執行緒執行, 不能呼叫 Unity API」。

### 3.6 追查

- `DebugName`: 用於錯誤回報與追查訊息。
- `SubscriberCount`: 目前有效的訂閱數。
- `IsTraceEnabled` 為 true 時, 每次 Publish 以 `Log.Format(LogLevel.Normal, "[Event] {0} -> {1} subscriber(s)", DebugName, count)` 記錄。**不記錄事件內容** (需要 `ToString`, 會配置)。Log 關閉時 `Log.Format` 不配置, 所以 Publish 仍維持零配置。不得使用 `Log.Debug` (函式庫內部規則)。

---

## 4. Runtime

### 4.1 生命週期綁定

```csharp
namespace Torben.Events
{
    public static class EventSubscriptionUnityExtensions
    {
        /// 在 owner 所在的 GameObject 被 Destroy 時自動 Dispose。回傳原 subscription 方便串接。
        public static EventSubscription BindTo(this EventSubscription subscription, UnityEngine.Component owner);
    }
}
```

| 項目 | 規格 |
|---|---|
| 實作 | 在 owner 的 GameObject 上取得或加入一個隱藏元件 (例如 `EventSubscriptionBinder`, `hideFlags = HideInInspector`), 該元件保存訂閱清單, 在 `OnDestroy` 時全部 Dispose |
| 同一個 GameObject 多次 BindTo | 共用同一個隱藏元件 |
| `owner` 為 null 或已被 Destroy | 丟 `ArgumentNullException` / 立即 Dispose 並丟 `InvalidOperationException` (擇一, 文件說明) |
| `subscription` 為 null | 丟 `ArgumentNullException` |
| 停用 (`OnDisable`) | v1 **不** 提供綁定停用的版本。停用中的物件仍會收到事件; 需要時在教學文件說明以 `EventSubscriptionGroup` 搭配 `OnEnable` / `OnDisable` 的寫法 |
| 配置 | 只在呼叫時配置 (第一次加入元件、清單擴充) |

### 4.2 錯誤回報安裝

- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 將 `EventErrors.Handler` 設為以 `UnityEngine.Debug.LogException` 輸出, 訊息第一行包含頻道名稱 (比照 Log 的 `ReportSinkError` 做法, 不包 `InnerException` 以免頻道名稱消失)。
- 以 `=` 指定, 進入 Play 都重設 (關閉 Domain Reload 也不累積)。
- 不得經過 `Log` 輸出 (避免事件 → Log → 事件的循環可能)。

### 4.3 不提供

- 全域匯流排、ScriptableObject 事件頻道、`UnityEvent` 橋接、Inspector 接線 (第 9 節)。

---

## 5. 範例 (`Scripts/Samples/Events/`)

範例是教學文件的實作依據, 必須能直接在 Editor 開啟執行。

### 5.1 情境: 門打開 → 多個反應

| 角色 | 內容 |
|---|---|
| 事件 | `readonly struct DoorOpened { int DoorId; }` |
| 發布者 | `SampleDoor` (按鍵或 UI 按鈕觸發開門, 只持有 `IEventPublisher<DoorOpened>`) |
| 訂閱者 | `SampleDoorLight` (燈亮)、`SampleDoorSound` (播放或以 Log 顯示「播放音效」)、`SampleDoorCounter` (計數顯示) — 各自只持有 `IEventSource<DoorOpened>`, 以 `BindTo(this)` 綁定 |
| Composition Root | `SampleEventInstaller` (MonoBehaviour): 建立 `EventChannel<DoorOpened>`, 把它交給上面各物件 (以初始化方法注入, 不使用 `FindObjectOfType` / 單例) |
| 場景 | 以 Editor 選單或程式建立 (比照 Calculator 範例的 SceneBuilder 做法), 選單放 `TorbenTools/Events/` |

- 範例中至少有一個訂閱者會在執行中被 Destroy (例如 Counter 在計數達到 3 次後自毀), 用來示範 `BindTo` 自動取消。
- 範例開啟 `IsTraceEnabled`, 讓使用者在 GUI 視窗看到發布紀錄。

### 5.2 練習用的空位

範例需留下讓使用者自己加入新反應的明確位置 (例如 Installer 中有註解 `// 練習: 在這裡加入你的訂閱者`), 對應快速使用流程的練習題 (7.1)。

---

## 6. 測試

### 6.1 dotnet (Core)

| 分類 | 測試 |
|---|---|
| 基本 | 多個訂閱者都收到; 依訂閱順序; 沒有訂閱者時 Publish 不出錯; 事件內容正確傳遞 |
| 取消 | Dispose 後不再收到; 重複 Dispose 不出錯; `IsActive`、`SubscriberCount` 正確 |
| 發布中變更 | 發布中訂閱不在本次被呼叫; 發布中取消尚未輪到的訂閱, 本次不被呼叫; 發布中取消自己 |
| 例外 | 第一個訂閱者拋例外時第二個仍收到; 發布端不收到例外; 同一訂閱連續失敗只回報一次; `Handler` 為 null 不出錯; `Handler` 自己拋例外不傳回 |
| 巢狀 | 回呼中發布其他事件; 回呼中發布同一事件 (有終止條件); 超過深度上限時停止並回報一次 |
| 群組 | Dispose 取消全部; 已 Dispose 的群組 Add 時立即取消 |
| 同一 callback | 訂閱兩次會被呼叫兩次, 各自可取消 |
| 追查 | `IsTraceEnabled` 時經由 Log 記錄 (以測試 sink 驗證); 未開啟時不記錄 |
| 配置 | 若測試框架支援 `System.GC.GetAllocatedBytesForCurrentThread()`, 驗證 Publish (有訂閱者、Log 關閉) 不配置; 不支援則在回報中說明 |

### 6.2 PlayMode

| 測試 |
|---|
| `BindTo` 後 Destroy GameObject (等一幀), 訂閱失效且 `SubscriberCount` 減少 |
| 同一個 GameObject 多次 `BindTo` 只有一個隱藏元件, 全部在 Destroy 時取消 |
| `BindTo` 的 null / 已銷毀 owner 行為符合 4.1 |
| 沒有綁定的訂閱者被 Destroy 後發布: 不中斷其他訂閱者, Console 出現一次例外回報 (以 `LogAssert.Expect`) |
| 範例場景: 開門後三個訂閱者都反應; Counter 自毀後再開門不出錯 |

---

## 7. 教學文件 (主要交付物)

兩份文件都以 **使用者 (沒有事件開發經驗的開發者)** 為讀者, 不是給 AI 的規格。中文撰寫, 半形標點 + 空格 (專案慣例)。

### 7.1 快速使用流程: `docs/manual/event-quickstart.md`

目標: **照著做就會用**, 不解釋原理。

| 要求 | 說明 |
|---|---|
| 篇幅 | 約兩頁內 |
| 開頭 | 一句話說明事件適合做什麼, 加上「什麼時候用」的判斷表 (直接呼叫 / 事件 / `TransitionRequest` / `Output`, 各一個遊戲中的例子) |
| 步驟 | 編號步驟, 每一步: 要做什麼 + 可直接複製的程式碼 + 做完之後應該看到什麼。至少涵蓋: 定義事件 struct → 在 Installer 建立頻道 → 把發布介面交給發布者並發布 → 把訂閱介面交給訂閱者並 `Subscribe(...).BindTo(this)` → 執行確認 |
| 停用時的寫法 | 一小段: 需要「停用時不接收」時改用 `EventSubscriptionGroup` + `OnEnable` / `OnDisable` |
| 常見錯誤 | 表格: 症狀 → 原因 → 修正。至少包含: 忘記 `BindTo` (Console 出現例外回報)、在 Installer 之前就發布 (沒有反應)、用事件控制 StateMachine 流程 (違反原則)、事件互相觸發造成無限循環 (深度上限回報) |
| 練習 | 以範例場景為基礎: 「門打開時讓粒子特效播放」, 列出要新增的檔案與要修改的位置, 並說明完成的樣子 |
| 程式碼來源 | 文件中的每段程式碼必須與範例 (第 5 節) 一致或可編譯; 在文件末尾列出對應的範例檔案路徑 |

### 7.2 完整筆記: `docs/note/event-system-notes.md`

目標: **說明運作原理**, 讓使用者在遇到問題或想擴充時能理解內部。從基礎開始, 不假設讀者懂事件。

必須包含的章節:

| 章節 | 內容 |
|---|---|
| 為什麼需要事件 | 直接呼叫與事件的差別 (以「玩家受傷 → 血條、音效、鏡頭」為例, 兩種寫法對照); 各自適合的情況與代價 |
| 「隱藏依賴」問題 | 引用設計原則文件的顧慮; 說明單例匯流排與散落的 `+=` 為什麼看不出誰在聽 |
| 本系統的解法 | 頻道實例、發布 / 訂閱兩個介面、Composition Root 集中接線; 對照圖或表格 |
| 內部運作 | copy-on-write 訂閱者陣列 (為什麼發布時不用鎖、為什麼不配置)、有效旗標 (發布中取消的處理)、例外隔離與只回報一次、巢狀深度上限、執行緒行為。每一項都說明「為什麼這樣做」與「不這樣做會發生什麼」 |
| 生命週期 | `BindTo` 的原理 (隱藏元件 + `OnDestroy`); 為什麼 v1 不綁定停用; 忘記時的保險 (例外隔離) |
| 配置行為 | 哪些操作配置、哪些不配置, 以及原因 (struct、`in`、快照陣列、lambda 捕捉變數會在訂閱時配置) |
| 與 StateMachine 的關係 | 事件 / `Output` / `TransitionRequest` / Guard 的分工表; 「收到事件後要轉換狀態」時, 每種事件只能有一個訂閱者負責送出 `Request`, 並在 Composition Root 接線; 想知道 StateMachine 轉換了時, 在 Project 層輪詢 `CurrentSerialNumber` 再自行發布 |
| 與 Log 系統的對照 | sink 與訂閱者的相同與不同之處 (使用者已理解 Log 的 sink, 以此為橋樑) |
| 其他做法的比較 | C# `event`、`UnityEvent`、單例匯流排、ScriptableObject 頻道: 各自的優缺點, 以及 v1 為什麼不採用 |
| 未來可能的擴充 | 延後送達 (佇列)、`UnityEvent` 橋接、Inspector 接線、Editor 檢視視窗: 各自解決什麼問題、何時值得做 |
| 名詞表 | 發布、訂閱、頻道、handle、Composition Root、copy-on-write、快照、巢狀發布 等 |

### 7.3 其他文件

| 文件 | 內容 |
|---|---|
| `docs/event-system.md` | API 參考 (比照 `docs/log-system.md`): 型別、行為規則、配置行為、限制 |
| `AGENTS.md` | 新增「Event 使用規則」: 只用於通知事實; 不建立全域匯流排或單例存取頻道; 訂閱必須 `BindTo` 或加入群組; StateMachine Core 不得加入事件; 每種事件最多一個訂閱者送出 `TransitionRequest`; 回呼中不做耗時工作 |
| `docs/event-system-status-report.md` | 現況報告 (比照 Log 的現況報告格式, 給後續設計用 AI): 完成狀況、資料結構、測試、缺口、刻意不做 |

---

## 8. 完成標準

全部符合才算完成:

1. dotnet 與 PlayMode 測試全部通過, 第 6 節列出的測試都存在 (配置測試依框架支援情況)。
2. Core 的 Events 程式不引用 `UnityEngine` / `UnityEditor`; 函式庫內部沒有 `Log.Debug` 呼叫。
3. 程式中沒有任何靜態的頻道實例或全域匯流排 (`EventErrors.Handler` 除外)。
4. 範例場景可由 `TorbenTools/Events/` 選單建立並執行; 開門後三個訂閱者反應, Counter 自毀後再開門不出錯, GUI 視窗看得到追查紀錄 (人工確認)。
5. 第 7 節的五份文件都已建立或更新; 快速使用流程中的程式碼與範例一致。
6. **人工確認 (使用者)**: 只看 `event-quickstart.md`, 完成文件中的練習 (粒子特效反應)。過程中若有看不懂或需要回頭問 AI 的地方, 記錄下來並修正文件。
7. 回報: 修改的檔案清單、測試數量、與本需求不同的地方及理由、配置測試結果、遇到但未處理的問題。

---

## 9. 不在本次範圍

- 全域事件匯流排、單例存取。
- 延後送達 (事件佇列)。
- `UnityEvent` 橋接、ScriptableObject 事件頻道、Inspector 接線。
- Editor 事件檢視視窗。
- 綁定 `OnDisable` 的生命週期版本。
- 事件過濾、優先順序。
- 執行緒切換 (背景執行緒發布轉到主執行緒)。
- StateMachine Core 的任何修改。
