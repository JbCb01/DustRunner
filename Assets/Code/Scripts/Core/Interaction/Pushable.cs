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
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void Push(Vector3 direction)
    {
        if (!IsPushable || Rigidbody.isKinematic) return;
        Rigidbody.AddForce(direction * PushPower, ForceMode.VelocityChange);
    }
}