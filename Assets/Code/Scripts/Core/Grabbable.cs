using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Grabbable : MonoBehaviour
{
    [Header("Grip Settings")]
    [Tooltip("How firmly the player holds this object. Lower values = loosely held (good for heavy items).")]
    public float GripStrength = 1f; 
    public float MaxHoldDistance = 3.0f;

    [Header("Physics Settings")]
    public float MoveP = 1000f; // Strength
    public float MoveD = 10f;   // Damping
    public float MaxMoveForce = 1000f;
    
    [Header("Rotation")]
    public float RotationSmoothSpeed = 10f;

    private Rigidbody _rb;
    private Vector3 _localGrabPoint;
    private float _originalDrag;
    private float _originalAngularDrag;

    public bool IsGrabbed { get; private set; }
    public Rigidbody Rigidbody => _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void BeginGrab(Vector3 hitPointWorld)
    {
        IsGrabbed = true;
        
        // Store where we grabbed it relative to the object center
        _localGrabPoint = transform.InverseTransformPoint(hitPointWorld);

        // Save original stats so we can restore them on drop
        _originalDrag = _rb.linearDamping;
        _originalAngularDrag = _rb.angularDamping;

        // Physics setup for holding
        _rb.useGravity = false;
        // We increase drag slightly so it doesn't jitter, but not too much or it feels like interacting with molasses
        _rb.linearDamping = 1f; 
        _rb.angularDamping = 1f;
    }

    public void UpdateGrab(Vector3 targetHoldPoint, float dt)
    {
        if (!IsGrabbed) return;

        // 1. Calculate where the grab point IS vs where it SHOULD BE
        Vector3 currentGrabPointWorld = transform.TransformPoint(_localGrabPoint);
        Vector3 error = targetHoldPoint - currentGrabPointWorld;

        // 2. Safety Release (If object gets stuck behind a wall)
        if (error.magnitude > MaxHoldDistance)
        {
            EndGrab();
            return;
        }

        // 3. PD Controller (The magic math)
        Vector3 pointVelocity = _rb.GetPointVelocity(currentGrabPointWorld);
        
        // Apply GripStrength to the P (Position) force
        float effectiveP = MoveP * GripStrength;
        
        Vector3 force = (error * effectiveP) - (pointVelocity * MoveD);
        force = Vector3.ClampMagnitude(force, MaxMoveForce);

        // 4. Apply Force
        _rb.AddForceAtPosition(force, currentGrabPointWorld);

        // 5. Rotation (Keep it stable)
        // We dampen rotation so it doesn't spin forever while holding
        _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, dt * RotationSmoothSpeed);
    }

    public void EndGrab()
    {
        IsGrabbed = false;
        
        // Restore Physics
        _rb.useGravity = true;
        _rb.linearDamping = _originalDrag;
        _rb.angularDamping = _originalAngularDrag;
        
        // Optional: Throw impulse?
        // You could add _rb.AddForce(cameraForward * throwForce, ForceMode.Impulse) here
    }
}