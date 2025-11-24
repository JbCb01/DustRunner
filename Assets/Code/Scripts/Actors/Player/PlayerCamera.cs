using UnityEngine;
using Unity.Cinemachine;

public class PlayerCamera : MonoBehaviour
{
    public CinemachineCamera VCam;
    public Camera Main;

    [Header("FOV Kick")]
    public float baseFov = 75f;
    public float sprintFov = 85f;
    public float fovLerpSpeed = 8f;

    private bool _sprinting;

    private void Start()
    {
        if (VCam != null)
            VCam.Lens.FieldOfView = baseFov;
    }

    private void LateUpdate()
    {
        if (!VCam) return;

        float targetFov = _sprinting ? sprintFov : baseFov;
        VCam.Lens.FieldOfView = Mathf.Lerp(
            VCam.Lens.FieldOfView,
            targetFov,
            1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime)
        );
    }

    public void SetSprinting(bool sprinting)
    {
        _sprinting = sprinting;
    }

    public Vector3 GetPlanarForward()
    {
        if (!VCam) return Vector3.forward;

        Vector3 fwd = VCam.transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0f ? fwd.normalized : Vector3.forward;
    }

    public Vector3 GetPlanarRight()
    {
        if (!VCam) return Vector3.right;

        Vector3 right = VCam.transform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0f ? right.normalized : Vector3.right;
    }
}
