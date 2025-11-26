using UnityEngine;

public class ClimbingState : State<Player>, IKCCState
{
    public ClimbingState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) {}

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // W/S (y axis) now means Up/Down
        Vector2 input = Owner.Input.Player.Move.ReadValue<Vector2>();
        
        currentVelocity = Vector3.up * (input.y * Owner.Settings.ClimbSpeed);

        // Exit condition
        if (Owner.Input.Player.Jump.WasPressedThisFrame())
        {
             // Jump off ladder
             currentVelocity = (Owner.transform.forward * -2f) + (Vector3.up * 2f);
             StateMachine.ChangeState(Owner.AirborneState);
        }
    }
}