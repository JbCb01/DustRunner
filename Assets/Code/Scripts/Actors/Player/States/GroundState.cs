using UnityEngine;

public class GroundState : State<Player>, IKCCState
{
    public GroundState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) {}

    public override void Enter()
    {
        Owner.Controller.SetCrouch(false, Owner.Settings.StandHeight, Owner.Settings.CapsuleRadius);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // 1. Read Inputs
        Vector2 inputVector = Owner.Input.Player.Move.ReadValue<Vector2>();
        bool isSprinting = Owner.Input.Player.Sprint.IsPressed();
        bool isCrouching = Owner.Input.Player.Crouch.IsPressed(); // Assuming you have this input binding

        // 2. Handle Crouch Logic (State drives the Controller)
        // We pass the settings values so Controller acts only as executor
        if (isCrouching)
        {
            Owner.Controller.SetCrouch(true, Owner.Settings.CrouchHeight, Owner.Settings.CapsuleRadius);
        }
        else
        {
            Owner.Controller.SetCrouch(false, Owner.Settings.StandHeight, Owner.Settings.CapsuleRadius);
        }

        // 3. Determine Target Speed
        float targetSpeed = Owner.Settings.WalkSpeed;
        
        if (isCrouching) 
            targetSpeed = Owner.Settings.CrouchSpeed;
        else if (isSprinting) 
            targetSpeed = Owner.Settings.SprintSpeed;

        if (inputVector == Vector2.zero) targetSpeed = 0f;

        // 4. Calculate Target Velocity
        Vector3 moveDirection = Vector3.zero;
        if (inputVector.magnitude > 0)
        {
            Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
            Vector3 cameraPlanarRight = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.right, Vector3.up).normalized;
            moveDirection = (cameraPlanarForward * inputVector.y + cameraPlanarRight * inputVector.x).normalized;
        }

        Vector3 targetVelocity = moveDirection * targetSpeed;

        // 5. Apply Velocity with Sharpness (Responsive movement)
        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround)
        {
            // Lerp towards target velocity for smooth acceleration/deceleration
            float sharpness = Owner.Settings.GroundMovementSharpness;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));
        }
        else
        {
            // We lost ground -> Transition to Air
            StateMachine.ChangeState(Owner.AirborneState);
            return; 
        }

        // 6. Jump
        if (Owner.Input.Player.Jump.WasPressedThisFrame() && !isCrouching) // Usually can't jump while crouching
        {
            DoJump(ref currentVelocity);
            Debug.Log("Jumping");
        }
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // Standard FPS Rotation: Body follows Camera View
        // Or Lethal Company style: Body follows movement input
        
        Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
        if (cameraPlanarForward.sqrMagnitude > 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(cameraPlanarForward);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, 1f - Mathf.Exp(-Owner.Settings.RotationSpeed * deltaTime));
        }
    }

    private void DoJump(ref Vector3 currentVelocity)
    {
        Vector3 jumpDirection = Owner.Controller.Motor.GroundingStatus.GroundNormal;
        float jumpVelocity = Mathf.Sqrt(2f * Owner.Settings.JumpHeight * -Owner.Settings.Gravity.y);
        
        // Reset vertical velocity for consistent jump height
        currentVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        currentVelocity += jumpDirection * jumpVelocity;

        Owner.Controller.Motor.ForceUnground();
        StateMachine.ChangeState(Owner.AirborneState);
    }
}