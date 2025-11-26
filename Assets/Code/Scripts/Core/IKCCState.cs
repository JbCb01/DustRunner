using UnityEngine;

public interface IKCCState
{
    void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime);
    void UpdateRotation(ref Quaternion currentRotation, float deltaTime);
}