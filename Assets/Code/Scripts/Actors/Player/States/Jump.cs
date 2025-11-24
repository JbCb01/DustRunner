using UnityEngine;

public class Jump : State<Player>
{
    public Jump(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.RequestJump();
    }

    public override void LogicUpdate()
    {
        if (!Owner.Controller.Motor.GroundingStatus.IsStableOnGround && Owner.Controller.JumpedThisFrame)
        {
            StateMachine.ChangeState(Owner.GetState<Fall>());
            return;
        }

        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround && !Owner.Controller.JumpedThisFrame)
        {
            Vector2 moveInput = Owner.Input.Player.Move.ReadValue<Vector2>();
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                StateMachine.ChangeState(Owner.GetState<Idle>());
            }
            else if (Owner.Input.Player.Sprint.IsPressed())
            {
                StateMachine.ChangeState(Owner.GetState<Sprint>());
            }
            else
            {
                StateMachine.ChangeState(Owner.GetState<Move>());
            }
        }
    }
}