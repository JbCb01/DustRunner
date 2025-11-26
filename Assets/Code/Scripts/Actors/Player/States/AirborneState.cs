using UnityEngine;

public class AirborneState : State<Player>, IKCCState
{
    public AirborneState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) { }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 1. Read Input
        Vector2 inputVector = Owner.Input.Player.Move.ReadValue<Vector2>();

        // 2. Apply Gravity (From Settings)
        currentVelocity += Owner.Settings.Gravity * deltaTime;

        // 3. Air Control (Allow slight movement adjustments in air)
        if (inputVector.magnitude > 0)
        {
            Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
            Vector3 cameraPlanarRight = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.right, Vector3.up).normalized;
            Vector3 targetMovement = (cameraPlanarForward * inputVector.y + cameraPlanarRight * inputVector.x).normalized;

            // Add air acceleration directly to velocity
            Vector3 airAccel = targetMovement * Owner.Settings.AirAccelerationSpeed * deltaTime;
            
            // Ensure we don't accelerate beyond max speed if needed, but for now simple addition
            currentVelocity += new Vector3(airAccel.x, 0, airAccel.z);
        }

        // 4. Drag (Air Resistance)
        currentVelocity *= 1f / (1f + (Owner.Settings.Drag * deltaTime));

        // 5. Check Landing
        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround && currentVelocity.y <= 0.1f)
        {
            StateMachine.ChangeState(Owner.GroundState);
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // Optional: Allow rotation in air? Usually yes for FPS.
        Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
        if (cameraPlanarForward.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraPlanarForward);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 1f - Mathf.Exp(-Owner.Settings.RotationSpeed * deltaTime));
        }
    }
}