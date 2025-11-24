using UnityEngine;

public class Fall : State<Player>
{
    public Fall(Player owner, StateMachine<Player> machine) : base(owner, machine) { }

    public override void Enter()
    {
        Owner.Controller.SetMovementSpeeds(Owner.Controller.WalkStableMoveSpeed, Owner.Controller.WalkAirMoveSpeed);
    }

    public override void LogicUpdate()
    {
        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround)
        {
            Vector2 moveInput = Owner.Input.Player.Move.ReadValue<Vector2>();

            if (Owner.Input.Player.Crouch.IsPressed())
            {
                StateMachine.ChangeState(Owner.GetState<Crouch>());
                return;
            }

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
            return;
        }
    }
}