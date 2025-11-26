using UnityEngine;

public class GroundState : PlayerActiveState, IKCCState
{
    private float _jumpRequestTime = -1f;
    private const float JumpBufferDuration = 0.15f;

    public GroundState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) {}

    public override void Enter()
    {
        Owner.Controller.SetCrouch(false, Owner.Settings.StandHeight, Owner.Settings.CapsuleRadius);
        _jumpRequestTime = -1f;
    }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (Owner.Input.Player.Jump.WasPressedThisFrame())
        {
            _jumpRequestTime = Time.time;
        }
        HandleCameraHeight();
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Vector2 inputVector = Owner.Input.Player.Move.ReadValue<Vector2>();
        bool isSprinting = Owner.Input.Player.Sprint.IsPressed();
        bool isCrouching = Owner.Input.Player.Crouch.IsPressed();

        bool pendingJump = (Time.time - _jumpRequestTime) <= JumpBufferDuration;

        if (pendingJump && !isCrouching && Owner.Controller.Motor.GroundingStatus.IsStableOnGround)
        {
            _jumpRequestTime = -1f;
            DoJump(ref currentVelocity);
            
            Owner.Controller.Motor.ForceUnground(); 
            StateMachine.ChangeState(Owner.AirborneState);
            return;
        }

        if (isCrouching)
        {
            Owner.Controller.SetCrouch(true, Owner.Settings.CrouchHeight, Owner.Settings.CapsuleRadius);
        }
        else
        {
            Owner.Controller.SetCrouch(false, Owner.Settings.StandHeight, Owner.Settings.CapsuleRadius);
        }

        float targetSpeed = Owner.Settings.WalkSpeed;
        if (isCrouching) targetSpeed = Owner.Settings.CrouchSpeed;
        else if (isSprinting) targetSpeed = Owner.Settings.SprintSpeed;

        if (inputVector == Vector2.zero) targetSpeed = 0f;

        Vector3 moveDirection = Vector3.zero;
        if (inputVector.magnitude > 0)
        {
            Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.forward, Vector3.up).normalized;
            Vector3 cameraPlanarRight = Vector3.ProjectOnPlane(Owner.Camera.Main.transform.right, Vector3.up).normalized;
            moveDirection = (cameraPlanarForward * inputVector.y + cameraPlanarRight * inputVector.x).normalized;
        }

        Vector3 targetVelocity = moveDirection * targetSpeed;

        if (Owner.Controller.Motor.GroundingStatus.IsStableOnGround)
        {
            float sharpness = Owner.Settings.GroundMovementSharpness;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-sharpness * deltaTime));
        }
        else
        {
            StateMachine.ChangeState(Owner.AirborneState);
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

    private void DoJump(ref Vector3 currentVelocity)
    {
        Vector3 jumpDirection = Owner.Controller.Motor.GroundingStatus.GroundNormal;
        
        if (Vector3.Dot(jumpDirection, Vector3.up) < 1f) 
        {
            jumpDirection = Vector3.up; 
        }

        float jumpVelocity = Mathf.Sqrt(2f * Owner.Settings.JumpHeight * -Owner.Settings.Gravity.y);
        
        currentVelocity = new Vector3(currentVelocity.x, 0, currentVelocity.z);
        currentVelocity += jumpDirection * jumpVelocity;
    }

    private void HandleCameraHeight()
    {
        bool isCrouching = Owner.Input.Player.Crouch.IsPressed();
        float targetHeight = isCrouching ? Owner.Settings.CrouchHeight * 0.9f : Owner.Settings.StandHeight * 0.9f;
        
        Vector3 currentLocalPos = Owner.Head.localPosition;
        float newHeight = Mathf.Lerp(currentLocalPos.y, targetHeight, 1f - Mathf.Exp(-15f * Time.deltaTime));
        
        Owner.Head.localPosition = new Vector3(currentLocalPos.x, newHeight, currentLocalPos.z);
    }
}