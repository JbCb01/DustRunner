using System;
using UnityEngine;

[Serializable]
public class PlayerSettings
{
    [Header("Movement Speeds")]
    public float WalkSpeed = 10f;
    public float SprintSpeed = 15f;
    public float CrouchSpeed = 5f;
    public float ClimbSpeed = 4f;

    [Header("Physics & Sharpness")]
    public float GroundMovementSharpness = 15f;
    public float AirAccelerationSpeed = 10f;
    public float RotationSpeed = 15f;
    public Vector3 Gravity = new Vector3(0, -30f, 0);
    public float Drag = 0.1f;

    [Header("Jumping")]
    public float JumpHeight = 2f;

    [Header("Dimensions")]
    public float StandHeight = 2f;
    public float CrouchHeight = 1.2f;
    public float CapsuleRadius = 0.5f;
}