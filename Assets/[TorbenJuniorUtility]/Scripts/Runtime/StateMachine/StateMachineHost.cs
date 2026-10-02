using UnityEngine;

namespace Torben.StateMachine
{
    /// <summary>將同步狀態機接到 Unity 的啟用、Update 與停用 lifecycle.</summary>
    /// <remarks>
    /// 配置: 僅建立 Host 與機器時配置; Tick 的配置取決於 Core 與 Project 實作.
    /// 子類別不要自行宣告 Awake/OnEnable/Update/OnDisable, 否則 Unity 只呼叫子類別版本;
    /// 請改覆寫 OnHostAwake/OnHostEnabled/OnHostDisabled.
    /// 遮蔽 Awake 時機器會在 OnEnable 補建; 遮蔽 OnEnable/OnDisable 則不會自動 Start/Stop.
    /// </remarks>
    public abstract class StateMachineHost<TContext, TOutput> : MonoBehaviour
    {
        /// <summary>Awake 後建立的機器; Project 可透過此介面提出 Request 或讀取結果.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public IStateMachine<TContext, TOutput> Machine { get; private set; }

        /// <summary>建立此 Host 擁有的機器與固定 Flow.</summary>
        /// <remarks>配置: 僅初始化時配置; 每個 Host 應建立自己的可變 State 實例.</remarks>
        protected abstract IStateMachine<TContext, TOutput> CreateMachine();

        /// <summary>為每次啟用提供初始 Context.</summary>
        /// <remarks>配置: 依 Project 實作, 每次呼叫可能配置.</remarks>
        protected abstract TContext CreateInitialContext();

        /// <summary>機器建立後於 Awake 呼叫; 此時 Machine 已可用, 尚未 Start.</summary>
        /// <remarks>配置: 預設零配置; 覆寫的配置取決於 Project 實作.</remarks>
        protected virtual void OnHostAwake() { }

        /// <summary>每次 OnEnable 於嘗試 Start 之後呼叫.</summary>
        /// <remarks>配置: 預設零配置; 覆寫的配置取決於 Project 實作.</remarks>
        protected virtual void OnHostEnabled() { }

        /// <summary>每次 OnDisable 於 Stop 之前呼叫; 此時機器仍維持停用前的狀態.</summary>
        /// <remarks>配置: 預設零配置; 覆寫的配置取決於 Project 實作.</remarks>
        protected virtual void OnHostDisabled() { }

        /// <summary>建立機器, 不改寫 Core lifecycle.</summary>
        /// <remarks>配置: 僅初始化時配置; 無效設定的例外路徑配置.</remarks>
        protected void Awake()
        {
            EnsureMachine();
            OnHostAwake();
        }

        /// <summary>啟用時從 Stopped 啟動; 同一機器重新啟用時保留 Serial counter.</summary>
        /// <remarks>配置: 每次啟用可能配置 Context 或 lifecycle 所需物件.</remarks>
        protected void OnEnable()
        {
            // 子類別遮蔽 Awake 時, 在此補建機器
            EnsureMachine();
            if (Machine.Status == StateMachineStatus.Stopped)
                Machine.Start(CreateInitialContext());
            OnHostEnabled();
        }

        /// <summary>Running 時每個 Update 執行一個 Tick; Faulted 時停止自動 Tick.</summary>
        /// <remarks>配置: Host 自身零配置; Core 或 Project 實作每次呼叫可能配置.</remarks>
        protected void Update()
        {
            if (Machine.Status == StateMachineStatus.Running)
                Machine.Execute();
        }

        /// <summary>停用時 Stop; Faulted 的清理遵守 Core 不再 Exit 的規則.</summary>
        /// <remarks>配置: Host 自身零配置; Project 的 Exit 與例外路徑可能配置.</remarks>
        protected void OnDisable()
        {
            if (Machine == null) return;
            OnHostDisabled();
            Machine.Stop();
        }

        void EnsureMachine()
        {
            if (Machine != null) return;
            Machine = CreateMachine() ?? throw new System.InvalidOperationException(
                "A state machine host must create a machine.");
        }
    }
}
