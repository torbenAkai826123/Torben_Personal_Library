using Torben.StateMachine;

namespace Torben.StateMachine.Samples
{
    /// <summary>門鎖示範的 Project-owned Context.</summary>
    /// <remarks>配置: 僅建立實例時配置.</remarks>
    public sealed class DoorContext
    {
        /// <summary>保存這次請求是否持有鑰匙.</summary>
        /// <remarks>配置: 僅初始化時配置.</remarks>
        public DoorContext(bool hasKey) => HasKey = hasKey;

        /// <summary>這次請求是否持有鑰匙.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public bool HasKey { get; }
    }

    /// <summary>門鎖示範的 Project-owned 完成輸出.</summary>
    /// <remarks>配置: 僅建立實例時配置.</remarks>
    public sealed class DoorOutput
    {
        /// <summary>保存示範訊息.</summary>
        /// <remarks>配置: 僅初始化時配置; 不複製輸入字串.</remarks>
        public DoorOutput(string message) => Message = message;

        /// <summary>正常完成的示範訊息.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public string Message { get; }
    }

    /// <summary>等待開門請求的示範 State.</summary>
    /// <remarks>配置: 僅建立實例時配置.</remarks>
    public sealed class IdleState : State<DoorContext, DoorOutput>
    {
        /// <summary>等待狀態的固定識別值.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public override StateId Id => new StateId("idle");

        /// <summary>進入等待狀態.</summary>
        /// <remarks>配置: 零配置.</remarks>
        protected override void OnEnter(DoorContext context) { }

        /// <summary>等待 Project 提出請求.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public override void Execute() { }
    }

    /// <summary>進入時正常完成開門工作的示範 State.</summary>
    /// <remarks>配置: 僅建立實例及完成輸出時配置.</remarks>
    public sealed class DoorState : State<DoorContext, DoorOutput>
    {
        /// <summary>開門狀態的固定識別值.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public override StateId Id => new StateId("door");

        /// <summary>完成這次 activation 並提供輸出.</summary>
        /// <remarks>配置: 每次呼叫配置 DoorOutput.</remarks>
        protected override void OnEnter(DoorContext context) => Complete(new DoorOutput("Door opened"));

        /// <summary>完成後保留為 Current, 不自動轉換.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public override void Execute() { }
    }

    /// <summary>以同一份 Request Context 判斷是否允許開門.</summary>
    /// <remarks>配置: 僅建立實例時配置.</remarks>
    public sealed class HasKeyGuard : IGuard<DoorContext>
    {
        /// <summary>有鑰匙時 Allow, 否則 Deny.</summary>
        /// <remarks>配置: 零配置.</remarks>
        public GuardResult Evaluate(DoorContext context) => context.HasKey ? GuardResult.Allow : GuardResult.Deny;
    }

    /// <summary>建立交接版本的門鎖示範固定 Flow.</summary>
    /// <remarks>配置: 僅建置示範時配置.</remarks>
    public static class DoorFlow
    {
        /// <summary>每次建立獨立 State 實例, 避免多台機器共用可變 State.</summary>
        /// <remarks>配置: 每次呼叫配置 Builder、Flow、States 與 Guard.</remarks>
        public static IStateFlow<DoorContext, DoorOutput> Create()
        {
            return new StateFlowBuilder<DoorContext, DoorOutput>()
                .Initial(new StateId("idle"))
                .State(new IdleState())
                .State(new DoorState())
                .Transition(new TransitionId("open"), new StateId("idle"), new StateId("door"), new HasKeyGuard())
                .Build();
        }
    }
}
