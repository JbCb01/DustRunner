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
        
        var usable = GetComponent<Usable>();
        if (usable != null)
        {
            usable.OnUsed.AddListener(OnInteract);
        }
    }

    public void OnInteract(Player player)
    {
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        
        IsOpen = !IsOpen;
        
        Quaternion targetRotation;

        if (IsOpen)
        {
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
                Vector3 playerPos = player.transform.position;
                Vector3 doorToPlayer = playerPos - transform.position;
                float dot = Vector3.Dot(transform.forward, doorToPlayer.normalized);

                finalAngle = dot > 0 ? -OpenAngle : OpenAngle;
            }

            targetRotation = _closedRotation * Quaternion.Euler(0, finalAngle, 0);
        }
        else
        {
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
        _animationCoroutine = null;
    }
}