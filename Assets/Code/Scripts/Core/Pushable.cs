using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pushable : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Multiplier for the force applied by the player.")]
    public float PushPower = 2.0f;

    [Tooltip("If true, the object can be pushed.")]
    public bool IsPushable = true;

    public Rigidbody Rigidbody { get; private set; }

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
        // Ensure the Rigidbody reacts properly to physics but doesn't jitter
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Push(Vector3 direction)
    {
        if (!IsPushable || Rigidbody.isKinematic) return;

        // Apply force. ForceMode.VelocityChange is usually best for instant "snappy" response 
        // to character movement, ignoring the object's mass slightly for better game-feel.
        // You can switch to ForceMode.Force if you want mass to matter more.
        Rigidbody.AddForce(direction * PushPower, ForceMode.VelocityChange);
    }
}