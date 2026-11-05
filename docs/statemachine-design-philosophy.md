# StateMachine 開發偏好與設計原則

根據先前 Unity Struct Design 的架構討論，我會將這份摘要定位為 StateMachine 的設計理念與開發原則，而不只是列出類別或技術規格。

核心思想是：讓 StateMachine 負責明確的流程控制，而不是依賴分散的事件通知來驅動流程。

## 1. 核心設計理念

目前採用的方向是：

- 明確的狀態（State）。
- 明確的流程（Flow）。
- 明確的轉換條件（Guard）。
- 明確的轉換關係（Transition）。
- 明確的執行環境（Context）。
- 明確的執行結果（Output）。

希望透過這些結構，讓系統的行為具有可追蹤性、可預測性及可測試性。

設計時優先考慮：

1. Explicit over Implicit：明確描述依賴與流程，不依賴隱含行為。
2. Composition over Hidden Dependencies：透過組合與依賴注入建立系統，不讓各個 State 自行尋找其他系統。
3. Testability：各個組件能夠獨立測試，不必依賴完整 Unity 場景。
4. Separation of Concerns：Core 負責通用機制，Project 負責具體遊戲邏輯。

## 2. 為什麼不偏好使用 Event？

這是整個設計中特別重要的選擇。

並不是認為 Event 本身不好，而是認為它不適合成為 StateMachine 的主要流程控制機制。

主要有以下幾個理由。

### 2.1 隱藏依賴關係

Event 常見的使用方式：

```
public class StateA
{
    public event Action OnCompleted;

    public void Execute()
    {
        // 執行工作
        OnCompleted?.Invoke();
    }
}
```

其他系統則透過訂閱：

```
stateA.OnCompleted += HandleCompleted;
```

這種設計的問題在於：

- StateA 不知道誰正在監聽。
- StateA 的執行結果可能影響多個訂閱者。
- 單看 StateA 的程式碼，無法完整理解它的外部影響。
- 訂閱與解除訂閱的生命週期需要額外管理。

尤其在 Unity 中，如果訂閱與解除訂閱的時機不一致，還可能造成重複執行、殘留訂閱或物件生命週期問題。

偏好的方向是讓依賴關係直接出現在建構或執行介面中，而不是藏在訂閱關係裡。

### 2.2 流程控制不應分散

假設 StateA 完成後需要進入 StateB。

使用 Event 的方式可能是：

`StateA → Event → Subscriber → StateMachine → StateB`

流程控制散落在多個地方。

偏好的方式則是：

`StateA → Output → StateMachine → Transition → StateB`

讓 StateMachine 保持流程的唯一管理者。

這樣可以直接回答：

- 目前執行哪個 State？
- State 何時結束？
- 結束後產生什麼結果？
- 哪個 Transition 符合條件？
- 下一個 State 是什麼？

不必追查整個系統有哪些 Event 訂閱者。

### 2.3 Event 與 Output 的語意不同

這是先前討論中特別重要的區分。

| 項目 | Event | Output |
| --- | --- | --- |
| 語意 | 某件事情發生了 | State 執行後的結果 |
| 發生次數 | 可以重複發生 | 一次執行的最終結果 |
| 主要用途 | 通知其他系統 | 回報執行結果 |
| 流程控制 | 不適合直接負責 | 交由 StateMachine 判斷 |

例如：

- Event：玩家受到傷害。
- Event：播放動畫。
- Event：收到網路訊息。
- Output：攻擊 State 執行完成。
- Output：互動 State 回報成功或失敗。

因此，並不是所有事情都應該被包裝成 Output。

Event 負責描述發生的事情，Output 負責描述一次 State 執行的結果。

## 3. 偏好的架構組成

目前討論過的架構可以整理成以下關係：

Composition Root / Project Coordinator

負責組裝依賴、Flow、States 與 StateMachine

StateMachine Core

Flow

流程定義

State

狀態執行

Transition

狀態轉換

Guard

轉換條件

Context

執行環境

Output

執行結果

Project Logic

具體遊戲邏輯、資料與行為實作

這裡有幾個重要的責任界線：

| 組件 | 責任 |
| --- | --- |
| Core | 管理 State 的生命週期與轉換機制 |
| Project | 定義遊戲邏輯與具體資料 |
| Composition Root | 組裝各個依賴與系統 |
| Context | 提供執行時所需的環境資料 |
| Output | 回報 State 的執行結果 |

特別注意：

- Core 不應該知道具體 Project 的 Context 或 Output 類型。
- State 不應自行透過 `GetComponent` 或 `GameManager` 尋找依賴。
- 優先使用 Constructor Injection。
- 系統之間的依賴應由 Composition Root 統一組裝。

## 4. TransitionRequest 的定位

先前還討論過一個重要概念：`TransitionRequest`。

它的定位是 Project 向 StateMachine 提出的正式轉換請求。

也就是：

1. Project 邏輯判斷需要轉換。
2. 產生 TransitionRequest。
3. StateMachine 統一接收請求。
4. 由 StateMachine 負責後續轉換流程。

這樣可以避免不同 State 或 Project 系統各自操作 StateMachine 的內部狀態。

所有轉換都經過統一入口，方便追蹤與測試。

## 5. 開發時的偏好與限制

整理成實際開發規則：

| 項目 | 偏好 |
| --- | --- |
| 流程控制 | 由 StateMachine 統一管理 |
| 狀態轉換 | 明確的 Transition |
| 條件判斷 | Guard |
| 執行結果 | Output |
| 依賴注入 | Constructor Injection |
| 系統組裝 | Composition Root |
| 物件查找 | 不鼓勵 State 自行尋找依賴 |
| Event | 不作為主要流程控制機制 |
| Event Subscription | 避免隱藏式訂閱與解除訂閱 |
| Core | 保持獨立，不依賴 Project |
| 測試 | 優先支援獨立測試 |

另外，有一個重要的界線：

不使用 Event 驅動 StateMachine，不等於整個遊戲專案禁止 Event。

例如 UI 通知、音效觸發、外部系統通知等，仍然可能有使用 Event 的合理場景。

真正需要避免的是：

- 用 Event 取代 StateMachine 的流程管理。
- 透過隱藏的訂閱關係建立系統依賴。
- 讓 State 的執行結果無法明確追蹤。
- 讓多個訂閱者間接控制同一條狀態流程。

## 6. 最終設計哲學

我認為可以將這套架構的核心精神濃縮成以下幾句：

> StateMachine 應該管理流程，而不是被流程推著走。State 應該執行工作並回報結果，而不是自行決定整個系統的下一步。系統依賴應該明確建立，而不是隱藏在 Event Subscription 或物件查找之中。Core 應該提供通用機制，而不是認識具體遊戲專案。所有狀態轉換都應該具有可追蹤、可驗證的路徑。
> 

## 7. 我建議保留的一項設計判準

未來遇到是否使用 Event 的爭議，可以先問：

「這件事情是在通知某個事實，還是在要求系統改變執行流程？」

- 通知事實：Event 可能適合。
- 回報 State 執行結果：Output。
- 要求狀態轉換：TransitionRequest。
- 判斷是否允許轉換：Guard。
- 實際執行轉換：StateMachine。

這樣可以避免因為 Event 很方便，就讓它逐漸變成整個架構的隱性控制中心。

這份摘要可以作為 Unity Struct Design 的 StateMachine Design Philosophy 基礎文件。

下一步可以再獨立整理一份「StateMachine Core API 設計原則」，把上述理念轉換成介面、類別責任、生命週期與執行順序，作為實際開發時的檢查依據。