using System.Collections.Generic;
using KinematicCharacterController;
using UnityEngine;

[RequireComponent(typeof(KinematicCharacterMotor))]
public class PlayerController : MonoBehaviour, ICharacterController
{
    public Player Player { get; private set; }
    public KinematicCharacterMotor Motor;

    [Header("Capsule")]
    public float CapsuleRadius = 0.5f;
    public float CapsuleHeight = 2f;
    public float CrouchedCapsuleHeight = 1.2f;

    [Header("Movement Speeds")]
    public float WalkStableMoveSpeed = 10f;
    public float SprintStableMoveSpeed = 30f;
    public float CrouchStableMoveSpeed = 5f;
    public float WalkAirMoveSpeed = 6f;
    public float SprintAirMoveSpeed = 8f;

    [Header("Movement")]
    public float StableMovementSharpness = 15f;
    public float AirAccelerationSpeed = 15f;
    public float Drag = 0.1f;
    public Vector3 Gravity = new(0f, -30f, 0f);
    public float OrientationSharpness = 10f;

    [Header("Jumping")]
    public bool AllowJumpingWhenSliding = false;
    public float JumpUpSpeed = 5f;
    public float JumpScalableForwardSpeed = 4f;
    public float JumpPreGroundingGraceTime = 0f;
    public float JumpPostGroundingGraceTime = 0.15f;

    [Header("Visuals")]
    public Transform MeshRoot;

    [Header("Collisions")]
    public List<Collider> IgnoredColliders = new();


    private Vector3 _moveInputVector;
    private Vector3 _lookInputVector;
    private Quaternion _cameraRotation;
    private float _currentStableMoveSpeed;
    private float _currentAirMoveSpeed;
    private bool _jumpRequested;
    private bool _jumpConsumed;
    private bool _jumpedThisFrame;
    private float _timeSinceJumpRequested = Mathf.Infinity;
    private float _timeSinceLastAbleToJump;
    private Vector3 _internalVelocityAdd;
    private bool _shouldBeCrouching;
    private bool _isCrouching;
    private Collider[] _probedColliders = new Collider[8];
    public bool JumpedThisFrame => _jumpedThisFrame;

    private void Awake()
    {
        Motor.CharacterController = this;
        _currentStableMoveSpeed = WalkStableMoveSpeed;
        _currentAirMoveSpeed = WalkAirMoveSpeed;
    }

    public void Initialize(Player player)
    {
        Player = player;
        Motor.CharacterController = this;
    }

    public void SetInputs(ref PlayerCharacterInputs inputs)
    {
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveInput.x, 0f, inputs.MoveInput.y), 1f);
        _cameraRotation = inputs.CameraRotation;
        Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
        
        if (cameraPlanarDirection.sqrMagnitude == 0f)
        {
            cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
        }

        Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
        _moveInputVector = cameraPlanarRotation * moveInputVector;
        _lookInputVector = cameraPlanarDirection;

        if (inputs.JumpDown) RequestJump();
        if (inputs.CrouchDown) SetCrouch(true);
        else if (inputs.CrouchUp) SetCrouch(false);
    }

    private void LateUpdate()
    {
        HandleHeadPlayerOrientation();
    }

    public void SetMovementSpeeds(float stableMoveSpeed, float airMoveSpeed)
    {
        _currentStableMoveSpeed = stableMoveSpeed;
        _currentAirMoveSpeed = airMoveSpeed;
    }

    public void SetCrouch(bool shouldCrouch)
    {
        _shouldBeCrouching = shouldCrouch;

        if (shouldCrouch && !_isCrouching)
        {
            _isCrouching = true;
            Motor.SetCapsuleDimensions(CapsuleRadius, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
            if (MeshRoot != null)
                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
        }
    }

    public void RequestJump()
    {
        _jumpRequested = true;
        _timeSinceJumpRequested = 0f;
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
       if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
        {
            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;
            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
        }
    }

    private void HandleHeadPlayerOrientation()
    {
        Vector3 camForward = _cameraRotation * Vector3.forward;
        Vector3 localDir = transform.InverseTransformDirection(camForward);

        Vector3 localDirOnPlane = new(0f, 0f, new Vector2(localDir.z, localDir.x).magnitude);
        if (localDirOnPlane.sqrMagnitude < 0.0001f) return;

        float pitch = -Mathf.Atan2(localDir.y, localDirOnPlane.z) * Mathf.Rad2Deg;

        Quaternion targetLocal = Quaternion.Euler(pitch, 0f, 0f);
        Player.Head.localRotation = Quaternion.Slerp(
            Player.Head.localRotation,
            targetLocal,
            1f - Mathf.Exp(-OrientationSharpness * Time.deltaTime)
        );
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _jumpedThisFrame = false;

        if (Motor.GroundingStatus.IsStableOnGround)
        {
            _timeSinceLastAbleToJump = 0f;

            float currentVelocityMagnitude = currentVelocity.magnitude;
            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;

            float targetSpeed = _isCrouching ? CrouchStableMoveSpeed : _currentStableMoveSpeed;
            Vector3 targetMovementVelocity = reorientedInput * targetSpeed;

            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetMovementVelocity,
                1f - Mathf.Exp(-StableMovementSharpness * deltaTime)
            );
        }
        else
        {
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;
                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                // Limit air speed, but allow external velocity (like explosions) to exceed it
                if (currentVelocityOnInputsPlane.magnitude < _currentAirMoveSpeed)
                {
                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, _currentAirMoveSpeed);
                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                }
                else if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                }

                currentVelocity += addedVelocity;
            }

            // Gravity
            currentVelocity += Gravity * deltaTime;
            currentVelocity *= 1f / (1f + Drag * deltaTime);

        }

        if (_jumpRequested)
        {
            _timeSinceJumpRequested += deltaTime;
            if (CanJump())
            {
                PerformJump(ref currentVelocity);
            }
            else if (_timeSinceJumpRequested > JumpPreGroundingGraceTime)
            {
                _jumpRequested = false;
            }
        }

        if (_internalVelocityAdd != Vector3.zero)
        {
            currentVelocity += _internalVelocityAdd;
            _internalVelocityAdd = Vector3.zero;
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround)
        {
            _jumpConsumed = false;
        }

        if (_isCrouching && !_shouldBeCrouching)
        {
            Motor.SetCapsuleDimensions(CapsuleRadius, CapsuleHeight, CapsuleHeight * 0.5f);

            int hitCount = Motor.CharacterOverlap(
                Motor.TransientPosition,
                Motor.TransientRotation,
                _probedColliders,
                Motor.CollidableLayers,
                QueryTriggerInteraction.Ignore);

            if (hitCount > 0)
            {
                Motor.SetCapsuleDimensions(CapsuleRadius, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
            }
            else
            {
                _isCrouching = false;
                if (MeshRoot != null)
                    MeshRoot.localScale = Vector3.one;
            }
        }
    }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLanded();
        }
        else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
        {
            OnLeaveStableGround();
        }
    }

    private bool CanJump()
    {
        bool validGround = Motor.GroundingStatus.IsStableOnGround || (AllowJumpingWhenSliding && Motor.GroundingStatus.FoundAnyGround);
        return !_jumpConsumed && (validGround || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime);
    }

    private void PerformJump(ref Vector3 currentVelocity)
    {
        Vector3 jumpDirection = Motor.CharacterUp;
        if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
        {
            jumpDirection = Motor.GroundingStatus.GroundNormal;
        }

        Motor.ForceUnground();
        currentVelocity += jumpDirection * JumpUpSpeed;
        currentVelocity += _moveInputVector * JumpScalableForwardSpeed;

        _jumpRequested = false;
        _jumpConsumed = true;
        _jumpedThisFrame = true;
    }

    private void OnLanded()
    {
    }

    private void OnLeaveStableGround()
    {
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        if (IgnoredColliders != null && IgnoredColliders.Contains(coll))
            return false;

        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
        ref HitStabilityReport hitStabilityReport)
    {
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if(Player.Interaction != null)
        {
            Player.Interaction.ProcessPhysicalInteraction(hitCollider, Motor.Velocity);
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
    {
    }
}