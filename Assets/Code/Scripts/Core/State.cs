using System;

public abstract class State<TOwner> where TOwner : class
{
    protected readonly TOwner Owner;
    protected readonly StateMachine<TOwner> StateMachine;
    protected State(TOwner owner, StateMachine<TOwner> machine)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        StateMachine = machine ?? throw new ArgumentNullException(nameof(machine));
    }
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void HandleInput() { }
    public virtual void LogicUpdate() { }
    public virtual void PhysicsUpdate() { }
}