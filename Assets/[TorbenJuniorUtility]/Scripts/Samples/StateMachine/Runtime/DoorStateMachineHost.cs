using Torben.StateMachine;

namespace Torben.StateMachine.Samples
{
    /// <summary>可附加到 GameObject 的門鎖示範 Host.</summary>
    /// <remarks>配置: 僅初始化及提出示範請求時配置.</remarks>
    public sealed class DoorStateMachineHost : StateMachineHost<DoorContext, DoorOutput>
    {
        /// <summary>建立獨立示範機器與 Flow.</summary>
        /// <remarks>配置: 僅初始化時配置.</remarks>
        protected override IStateMachine<DoorContext, DoorOutput> CreateMachine()
            => new StateMachine<DoorContext, DoorOutput>(DoorFlow.Create());

        /// <summary>從沒有鑰匙的等待狀態開始.</summary>
        /// <remarks>配置: 每次呼叫配置 DoorContext.</remarks>
        protected override DoorContext CreateInitialContext() => new DoorContext(false);

        /// <summary>提出開門請求; 回傳值只表示 Pending slot 是否接受, 轉換由下一個 Update 處理.</summary>
        /// <remarks>配置: 每次呼叫配置 DoorContext.</remarks>
        public RequestResult RequestOpen(bool hasKey)
            => Machine.Request(new TransitionRequest<DoorContext>(new TransitionId("open"), new DoorContext(hasKey)));
    }
}
