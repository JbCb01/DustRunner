using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Usable))]
public class Door : MonoBehaviour
{
    public enum OpenDirection
    {
        AlwaysLeft,
        AlwaysRight,
        AutoAwayFromPlayer
    }

    [Header("Settings")]
    public float OpenAngle = 90f;
    public float AnimationSpeed = 2f;
    public OpenDirection Direction = OpenDirection.AutoAwayFromPlayer;

    [Header("State")]
    public bool IsOpen = false;

    private Quaternion _closedRotation;
    private Coroutine _animationCoroutine;

    private void Start()
    {
        _closedRotation = transform.localRotation;
        
        // Auto-wire the Usable event if forgotten in Editor
        var usable = GetComponent<Usable>();
        usable.OnInteract.AddListener(OnInteract);
    }

    public void OnInteract(Vector3 playerPos)
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        
        IsOpen = !IsOpen;
        
        Quaternion targetRotation;

        if (IsOpen)
        {
            // Calculate Open Rotation
            float finalAngle = OpenAngle;

            if (Direction == OpenDirection.AlwaysLeft)
            {
                finalAngle = -OpenAngle;
            }
            else if (Direction == OpenDirection.AlwaysRight)
            {
                finalAngle = OpenAngle;
            }
            else if (Direction == OpenDirection.AutoAwayFromPlayer)
            {
                // Calculate Dot Product to see if player is in front or behind
                Vector3 doorToPlayer = playerPos - transform.position;
                float dot = Vector3.Dot(transform.forward, doorToPlayer.normalized);

                // If dot > 0, player is in front (Z+), so open negative to swing away.
                // If dot < 0, player is behind (Z-), so open positive to swing away.
                finalAngle = dot > 0 ? -OpenAngle : OpenAngle;
            }

            targetRotation = _closedRotation * Quaternion.Euler(0, finalAngle, 0);
        }
        else
        {
            // Return to closed
            targetRotation = _closedRotation;
        }

        _animationCoroutine = StartCoroutine(AnimateDoor(targetRotation));
    }

    private IEnumerator AnimateDoor(Quaternion target)
    {
        while (Quaternion.Angle(transform.localRotation, target) > 0.1f)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * AnimationSpeed);
            yield return null;
        }
        transform.localRotation = target;
    }
}