using UnityEngine;

public class Move : State<Player>
{
    public Move(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.SetMovementSpeeds(Owner.Controller.WalkStableMoveSpeed, Owner.Controller.WalkAirMoveSpeed);
        Owner.Controller.SetCrouch(false);
    }

    public override void LogicUpdate()
    {
        Vector2 moveInput = Owner.Input.Player.Move.ReadValue<Vector2>();
        if (moveInput.sqrMagnitude < 0.0001f)
        {
            StateMachine.ChangeState(Owner.GetState<Idle>());
            return;
        }

        if (Owner.Input.Player.Sprint.IsPressed())
        {
            StateMachine.ChangeState(Owner.GetState<Sprint>());
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