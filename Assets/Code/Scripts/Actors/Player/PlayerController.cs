using KinematicCharacterController;
using UnityEngine;

[RequireComponent(typeof(KinematicCharacterMotor))]
public class PlayerController : MonoBehaviour, ICharacterController
{
    public Player Player; // FIXME: Should those in all controllers private to avoid too many references?
    public KinematicCharacterMotor Motor;

    private void Awake()
    {
        Motor.CharacterController = this;
    }

    public void Initialize(Player player)
    {
        Player = player;
    }

    public void SetCrouch(bool isCrouching, float height, float radius)
    {
        if (Mathf.Abs(Motor.Capsule.height - height) > 0.01f)
        {
            Motor.SetCapsuleDimensions(radius, height, height / 2f);
        }
    }

    // --- KCC Interface Implementation ---

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        Player.StateMachine.UpdateRotation(ref currentRotation, deltaTime);
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        Player.StateMachine.UpdateVelocity(ref currentVelocity, deltaTime);
    }

    public void BeforeCharacterUpdate(float deltaTime) { }
    
    public void PostGroundingUpdate(float deltaTime) { }
    
    public void AfterCharacterUpdate(float deltaTime) { }

    public bool IsColliderValidForCollisions(Collider coll) 
    {
        return true;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport) { }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // Interactions handled via Player Hub
        // if(_player.Interaction != null) ...
    }

    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    
    public void OnDiscreteCollisionDetected(Collider hitCollider) { }
}