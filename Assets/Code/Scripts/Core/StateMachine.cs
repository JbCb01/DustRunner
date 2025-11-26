using System;
using UnityEngine;

public class StateMachine<TOwner> where TOwner : class
{
    public StateMachine(TOwner owner)
    {
        Owner = owner;
    }

    public State<TOwner> CurrentState { get; private set; }
    public TOwner Owner { get; private set; }

    public event Action<State<TOwner>> OnStateChanged;

    // Cache to avoid casting every frame
    private IKCCState _currentKCCState;

    public void Initialize(State<TOwner> startState)
    {
        ChangeState(startState);
    }

    public void ChangeState(State<TOwner> newState)
    {
        if (newState == null || newState == CurrentState) return;

        CurrentState?.Exit();
        CurrentState = newState;
        
        // Check if the new state supports KCC physics
        _currentKCCState = newState as IKCCState;

        CurrentState.Enter();
        OnStateChanged?.Invoke(CurrentState);
    }

    public void LogicUpdate()
    {
        CurrentState?.LogicUpdate();
    }

    public void PhysicsUpdate()
    {
        CurrentState?.PhysicsUpdate();
    }

    // --- KCC Methods ---
    
    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _currentKCCState?.UpdateVelocity(ref currentVelocity, deltaTime);
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        _currentKCCState?.UpdateRotation(ref currentRotation, deltaTime);
    }
}