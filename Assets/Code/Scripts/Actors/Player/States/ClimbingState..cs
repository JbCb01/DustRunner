using UnityEngine;

public class ClimbingState : State<Player>, IKCCState
{
    public ClimbingState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) {}

    public override void Enter()
    {
        Owner.Controller.SetCrouch(false, Owner.Settings.StandHeight, Owner.Settings.CapsuleRadius);
    }

    public override void LogicUpdate()
    {
        if (Owner.Input.Player.Pause.WasPressedThisFrame())
        {
            StateMachine.ChangeState(Owner.MenuState);
            return; 
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector2 input = Owner.Input.Player.Move.ReadValue<Vector2>();
        currentVelocity = Vector3.up * (input.y * Owner.Settings.ClimbSpeed);

        if (Owner.Input.Player.Jump.WasPressedThisFrame())
        {
            currentVelocity = (Owner.transform.forward * -2f) + (Vector3.up * 2f);
            StateMachine.ChangeState(Owner.AirborneState);
        }
    }
}