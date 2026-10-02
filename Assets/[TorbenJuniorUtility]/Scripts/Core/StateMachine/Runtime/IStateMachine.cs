#nullable enable
namespace Torben.StateMachine
{

    /// <summary>同步狀態機契約.</summary>
    /// <remarks>配置: 參考型別建立時配置; 各操作的配置行為見成員說明.</remarks>
    public interface IStateMachine<TContext, TOutput>
    {
        /// <summary>同步狀態機契約.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        StateMachineStatus Status { get; }
        /// <summary>同步狀態機契約.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        StateId? CurrentStateId { get; }
        /// <summary>同步狀態機契約.</summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        long? CurrentSerialNumber { get; }
        /// <summary>
        /// 最近一次擷取的 activation 完成結果; 尚無結果時為 null.
        /// 只在 Enter (已分配 Serial 並提交 Current 後) 或 Execute 正常返回後擷取, 每個 activation 最多一次;
        /// callback 在 Complete 後拋例外不擷取, Exit 期間 (轉換或 Stop) 的 completion 不擷取.
        /// Request 不直接更新此值; 中斷、Stop、Fault 保留, 新一次 Start 才清空. 不保證屬於 Current, 請核對 StateId 與 SerialNumber.
        /// </summary>
        /// <remarks>配置: 本實作零配置; 介面或抽象成員的配置取決於實作.</remarks>
        StateResult<TOutput>? LastResult { get; }
        /// <summary>同步狀態機契約.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        void Start(TContext initialContext);
        /// <summary>
        /// 執行一個 Tick. 有 Pending 時取出並處理: 查找路徑與求值 Guards; 成功轉換依序呼叫 Old.Exit、Next.Enter,
        /// 本 Tick 不呼叫 Old 或 Next 的 Execute; 無路徑或 Guard Deny 時消耗 Request, 不 Exit.
        /// 沒有 Pending 時呼叫 Current.Execute. 兩種路徑都可能在 callback 正常返回後更新 LastResult.
        /// </summary>
        /// <remarks>配置: 每次呼叫可能配置; 轉換時列舉 Guards 及自訂 State/Guard/Flow 實作可能配置.</remarks>
        void Execute();
        /// <summary>Running 時 Exit Current 並清除 Current 與 Pending; Exit 期間的 completion 不擷取, LastResult 保留. Stopped 時 no-op; Faulted 時只清除, 不再 Exit.</summary>
        /// <remarks>配置: 內建正常路徑零配置; 自訂 lifecycle/Flow/Guard 實作可能每次呼叫配置; 例外路徑配置.</remarks>
        void Stop();
        /// <summary>
        /// Running 時寫入單一 Pending slot, 留待後續 Execute 處理; 不直接 Exit/Enter, 也不直接更新 LastResult.
        /// 尚未處理的 Pending 會被新 Request 取代 (Replaced). Accepted 不代表轉換成功. Stopped/Faulted 時回傳 Ignored.
        /// </summary>
        /// <remarks>配置: 本實作正常路徑零配置; 例外路徑可能配置.</remarks>
        RequestResult Request(TransitionRequest<TContext> request);
    }
}
