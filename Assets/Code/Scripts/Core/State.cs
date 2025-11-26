public abstract class State<TOwner> where TOwner : class
{
    protected TOwner Owner;
    protected StateMachine<TOwner> StateMachine;
    protected State(TOwner owner, StateMachine<TOwner> stateMachine)
    {
        Owner = owner;
        StateMachine = stateMachine;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void LogicUpdate() { } 
    public virtual void PhysicsUpdate() { }
}