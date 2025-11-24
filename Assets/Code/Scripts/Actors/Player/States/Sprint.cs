using UnityEngine;

public class Sprint : State<Player>
{
    public Sprint(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.SetMovementSpeeds(Owner.Controller.SprintStableMoveSpeed, Owner.Controller.SprintAirMoveSpeed);
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

        if (!Owner.Input.Player.Sprint.IsPressed())
        {
            StateMachine.ChangeState(Owner.GetState<Move>());
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