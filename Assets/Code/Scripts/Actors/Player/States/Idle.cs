using UnityEngine;

public class Idle : State<Player>
{
    public Idle(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.SetMovementSpeeds(Owner.Controller.WalkStableMoveSpeed, Owner.Controller.WalkAirMoveSpeed);
        Owner.Controller.SetCrouch(false);
    }

    public override void LogicUpdate()
    {
        Vector2 moveInput = Owner.Input.Player.Move.ReadValue<Vector2>();

        if (moveInput.sqrMagnitude > 0.0001f)
        {
            if (Owner.Input.Player.Sprint.IsPressed())
            {
                StateMachine.ChangeState(Owner.GetState<Sprint>());
            }
            else
            {
                StateMachine.ChangeState(Owner.GetState<Move>());
            }
            return;
        }

        if (Owner.Input.Player.Crouch.WasPressedThisFrame())
        {
            StateMachine.ChangeState(Owner.GetState<Crouch>());
            return;
        }

        if (Owner.Input.Player.Jump.WasPressedThisFrame() && Owner.Controller.Motor.GroundingStatus.IsStableOnGround)
        {
            StateMachine.ChangeState(Owner.GetState<Jump>());
            return;
        }

        if (!Owner.Controller.Motor.GroundingStatus.IsStableOnGround && !Owner.Controller.JumpedThisFrame)
        {
            StateMachine.ChangeState(Owner.GetState<Fall>());
            return;
        }
    }
}