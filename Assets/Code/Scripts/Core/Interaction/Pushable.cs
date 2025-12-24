using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pushable : MonoBehaviour
{
    [Header("Settings")]
    public float PushPower = 2.0f;
    public float ImpactPower = 5.0f;
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

    public void Hit(Vector3 point, Vector3 direction, float weaponForceMultiplier = 1f)
    {
        if (!IsPushable || Rigidbody.isKinematic) return;

        Rigidbody.AddForceAtPosition(ImpactPower * weaponForceMultiplier * direction, point, ForceMode.Impulse);
    }
}