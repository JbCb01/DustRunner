using Unity.VisualScripting;
using UnityEngine;

public class AirborneState : PlayerActiveState, IKCCState
{
    public AirborneState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) { }

    public override void LogicUpdate()
    {
        base.LogicUpdate();
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector2 inputVector = Owner.Input.Player.Move.ReadValue<Vector2>();
        currentVelocity += Owner.Settings.Gravity * deltaTime;

        if (inputVector.magnitude > 0)
        {
            Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
            Vector3 cameraPlanarRight = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.right, Vector3.up).normalized;
            Vector3 targetMovement = (cameraPlanarForward * inputVector.y + cameraPlanarRight * inputVector.x).normalized;
            Vector3 airAccel = targetMovement * Owner.Settings.AirAccelerationSpeed * deltaTime;
            currentVelocity += new Vector3(airAccel.x, 0, airAccel.z);
        }

        currentVelocity *= 1f / (1f + (Owner.Settings.Drag * deltaTime));
        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround && currentVelocity.y <= 0.1f)
        {
            StateMachine.ChangeState(Owner.GroundState);
            return;
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
        if (cameraPlanarForward.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraPlanarForward);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 1f - Mathf.Exp(-Owner.Settings.RotationSpeed * deltaTime));
        }
    }
}