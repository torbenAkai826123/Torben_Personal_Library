# Log 系統筆記： SINK 說明
先講最核心的一句：**sink 就是「Log 要送去哪裡」的出口。** `Log` 本身只負責把訊息包成 `LogEntry`，送去哪裡、怎麼顯示，交給各個 sink 處理。

## 1. 為什麼要拆成 sink：Core 不能碰 Unity

這是整個設計的起點。你的規則是 **Core 不得引用 `UnityEngine`**，但 Log 最終一定要呼叫 `UnityEngine.Debug.Log` 才能出現在 Console。

解法是讓 Core 只定義一個介面：

```csharp
public interface ILogSink { void Write(in LogEntry entry); }
```

Core 的 `Log` 只知道「有一群東西實作了 `Write`，我把訊息交給它們」，完全不知道 Unity 的存在。真正呼叫 `Debug.Log` 的 `DebugLogs` 放在 Runtime，啟動時把自己**登記**進 `Log`。依賴方向就變成 Runtime → Core，Core 保持乾淨。這種做法一般叫「依賴反轉」。

另外附帶三個好處：

| 好處 | 例子 |
|---|---|
| Core 可以用 `dotnet test` 測試 | 測試時登記一個「把收到的 entry 存進 List」的假 sink，就能檢查 Log 送出了什麼 |
| 一份訊息可以同時送往多處 | Console 和畫面視窗各拿一份，彼此不知道對方存在 |
| 新增輸出方式不用改 `Log` | 之後要寫檔，只要寫一個新的 sink 登記進去 |

## 2. `Log` 內部實際做的事

簡化之後大概是這樣（推測與實際程式碼結構接近，細節可能不同）：

```csharp
static volatile ILogSink[] sinks = new ILogSink[0];
static readonly object gate = new object();

static void Dispatch(in LogEntry entry)        // 每次 Log 呼叫
{
    var current = sinks;                        // 只讀一次參考
    for (int i = 0; i < current.Length; i++)
        current[i].Write(in entry);
}

public static void AddSink(ILogSink sink)       // 很少呼叫
{
    lock (gate)
    {
        var old = sinks;
        var next = new ILogSink[old.Length + 1]; // 配置新陣列
        old.CopyTo(next, 0);
        next[old.Length] = sink;
        sinks = next;                            // 整個換掉，不修改舊的
    }
}
```

## 3. copy-on-write 在解決什麼問題

問題來自**多執行緒**。`Log` 可能從任何執行緒被呼叫，例如 Unity 的 `logMessageReceivedThreaded` 會從背景執行緒觸發，`Task` 裡的程式也可能寫 Log。

假設用一般的 `List<ILogSink>`：

```
執行緒 A：for (i = 0; i < list.Count; i++) list[i].Write(...)   ← 跑到一半
執行緒 B：list.Remove(x)                                         ← 同時移除
→ A 可能讀到錯位的元素、超出範圍，或拋出「集合已修改」例外
```

常見的解法有兩種：

| 做法 | 每次 Log (很頻繁) | 增減 sink (很少) |
|---|---|---|
| 每次讀寫都 `lock` | 每次都要拿鎖，有成本，執行緒之間還會互等 | 拿鎖 |
| **copy-on-write** (現況) | 不拿鎖，直接讀陣列，零配置 | 複製一份新陣列再整個換掉 (會配置) |

copy-on-write 的核心規則是：**已經發出去的陣列永遠不會被修改。** 要變更就做一份新的，再把 `sinks` 指向新陣列。

這就像餐廳換菜單：不在客人手上那份菜單上塗改，而是印一份新的放到櫃台。已經拿到舊菜單的客人（正在跑迴圈的執行緒）照舊菜單點完不會出錯，下一位客人拿到的就是新菜單。

這樣做是把成本從頻繁的那一邊（每次 Log）移到很少發生的那一邊（啟動時加 sink），跟你的 GC 規則一致：「載入期可以配置，每幀路徑零配置」。

兩個細節：

- **`var current = sinks;` 只讀一次**：整個迴圈都用同一份陣列。如果每圈都重新讀 `sinks`，中途可能換成新陣列，長度就對不上了。
- **`volatile`**：確保其他執行緒能馬上看到「`sinks` 已經換成新陣列」，不會因為 CPU 快取或編譯器最佳化而一直讀到舊的。
- **`AddSink` 裡的 `lock`**：防止兩個執行緒同時新增，兩邊都從同一份舊陣列複製，結果其中一個 sink 被覆蓋掉。這個鎖只有寫入方需要，讀取方不用。

## 4. 「同步呼叫」代表什麼

報告寫「在呼叫端的執行緒上依序同步呼叫」，意思是：

```
你的 Update()
  └─ Log.Information("...")
       ├─ DebugLogs.Write       ← 跑完才往下
       ├─ LogHistorySink.Write  ← 跑完才往下
       └─ return 回你的 Update
```

沒有佇列，也沒有背景執行緒。好處是簡單、順序固定；代價是每個 sink 有多慢，你的 `Update` 就跟著慢多少。所以 sink 的 `Write` 要寫得很輕，寫檔這種慢的操作不適合直接放在 `Write` 裡。

另外報告第 4.2 節提到一個缺口：**沒有例外隔離。** 如果某個 sink 拋出例外，後面的 sink 收不到這筆訊息，例外還會一路傳回你的 `Update`。也就是說，Log 系統的錯誤可能讓遊戲邏輯中斷。目前只有兩個你自己寫的 sink，風險不大；之後如果加入自訂 sink，就應該考慮在迴圈裡加 `try/catch`。

## 5. 安裝時機

```
進入 Play
  └─ SubsystemRegistration (最早)
       DebugLogs.Install():
         Log.ClearSinks()                 ← 清空，避免關閉 Domain Reload 時殘留
         Log.AddSink(new DebugLogs())
         Log.AddSink(DebugWindow 的歷史紀錄)
  └─ BeforeSceneLoad
  └─ AfterSceneLoad ...
```

所以自訂 sink 要在 `BeforeSceneLoad` 之後才加入，太早加會被 `ClearSinks()` 清掉。

## 整理

| 概念 | 一句話 |
|---|---|
| sink | Log 的出口，`Log` 只管產生訊息，送去哪裡由 sink 決定 |
| 為什麼要有 | Core 不能引用 Unity，只能透過介面讓 Runtime 登記進來 |
| 多個 sink | 同一筆訊息依序交給每個出口 (Console、視窗紀錄、自訂) |
| copy-on-write | 要變更 sink 清單時換一份新陣列，不改舊的 → 讀取不用鎖、不配置 |
| `volatile` | 讓其他執行緒馬上看到新陣列 |
| 同步呼叫 | sink 在你的執行緒上直接執行，慢的 sink 會拖慢呼叫端 |
| 已知缺口 | 某個 sink 拋例外時，會中斷後面的 sink，並傳回呼叫端 |