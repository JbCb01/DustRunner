using UnityEngine;

public class Crouch : State<Player>
{
    public Crouch(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.SetMovementSpeeds(Owner.Controller.CrouchStableMoveSpeed, Owner.Controller.WalkAirMoveSpeed);
        Owner.Controller.SetCrouch(true);
    }

    public override void Exit()
    {
        Owner.Controller.SetCrouch(false);
    }

    public override void LogicUpdate()
    {
        Vector2 moveInput = Owner.Input.Player.Move.ReadValue<Vector2>();

        if (Owner.Input.Player.Crouch.WasPressedThisFrame())
        {
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                StateMachine.ChangeState(Owner.GetState<Idle>());
            }
            else
            {
                StateMachine.ChangeState(Owner.GetState<Move>());
            }
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