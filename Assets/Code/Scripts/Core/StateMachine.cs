using System;

public class StateMachine<TOwner> where TOwner : class {
    public State<TOwner> CurrentState { get; private set; }
    public State<TOwner> PreviousState { get; private set; }
    public TOwner Owner { get; }
    public StateMachine(TOwner owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }
    public void Initialize(State<TOwner> startState)
    {
        if (CurrentState != null) throw new InvalidOperationException("Machine already started.");
        CurrentState = startState ?? throw new ArgumentNullException(nameof(startState));
        CurrentState.Enter();
    }
    public void ChangeState(State<TOwner> newState)
    {
        if (newState == null || newState == CurrentState) return;

        CurrentState?.Exit();
        PreviousState = CurrentState;
        CurrentState = newState;
        CurrentState?.Enter();
    }

    public void Update()
    {
        CurrentState?.LogicUpdate();
    }

    public void FixedUpdate()
    {
        CurrentState?.PhysicsUpdate();
    }
}
