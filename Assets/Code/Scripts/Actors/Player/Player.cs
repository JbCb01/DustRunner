using System;
using System.Collections.Generic;
using UnityEngine;

public struct PlayerCharacterInputs
{
    public Vector2 MoveInput;
    public bool JumpDown;
    public bool CrouchDown;
    public bool CrouchUp;
    public Quaternion CameraRotation;
}

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent(typeof(PlayerUI))]
[RequireComponent(typeof(PlayerCamera))]
public class Player : MonoBehaviour
{
    [Header("Components")]
    public PlayerController Controller;
    public PlayerInteraction Interaction;
    public PlayerUI UI;
    public PlayerCamera Camera;
    public Transform Head;


    public PlayerControls Input { get; private set; }
    public StateMachine<Player> StateMachine { get; private set; }
    public Dictionary<Type, State<Player>> States { get; private set; }

    private void Awake()
    {
        Controller.Initialize(this);
        Interaction.Initialize(this);
        UI.Initialize(this);

        Input = new PlayerControls();
        StateMachine = new StateMachine<Player>(this);
        States = new Dictionary<Type, State<Player>>
        {
            [typeof(Idle)] = new Idle(this, StateMachine),
            [typeof(Move)] = new Move(this, StateMachine),
            [typeof(Sprint)] = new Sprint(this, StateMachine),
            [typeof(Fall)] = new Fall(this, StateMachine),
            [typeof(Crouch)] = new Crouch(this, StateMachine),
            [typeof(Jump)] = new Jump(this, StateMachine)
        };
    }

    private void Start()
    {
        StateMachine.Initialize(GetState<Idle>());
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        Input?.Enable();
    }

    private void OnDisable()
    {
        Input?.Disable();
    }



    private void Update()
    {
        StateMachine.Update();

        HandlePause();
        HandleInput();
        
        Interaction.UpdateInteractionLogic();
    }
    
    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
    }

    private void HandlePause()
    {
        if (Input.Player.Pause.WasPressedThisFrame())
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    private void HandleInput()
    {
        if (Controller == null || Interaction == null) return;

        bool interactPressed = Input.Player.Interact.WasPressedThisFrame();
        bool interactHeld = Input.Player.Interact.IsPressed();
        Vector2 moveInput = Input.Player.Move.ReadValue<Vector2>();
        
        PlayerCharacterInputs characterInputs = new()
        {
            MoveInput = moveInput,
            JumpDown = Input.Player.Jump.WasPressedThisFrame(),
            CrouchDown = Input.Player.Crouch.WasPressedThisFrame(),
            CrouchUp = Input.Player.Crouch.WasReleasedThisFrame(),
            CameraRotation = Camera.Main != null ? Camera.Main.transform.rotation : Quaternion.identity
        };

        Controller.SetInputs(ref characterInputs);
        Interaction.SetInputs(interactPressed, interactHeld);
    }
    public T GetState<T>() where T : State<Player>
    {
        return (T)States[typeof(T)];
    }
}