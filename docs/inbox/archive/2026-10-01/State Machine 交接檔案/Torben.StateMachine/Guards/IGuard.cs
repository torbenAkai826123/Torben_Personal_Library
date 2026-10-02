#nullable enable
namespace Torben.StateMachine
{

    public interface IGuard<TContext>
    {
        GuardResult Evaluate(TContext context);
    }
}
