using System;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerInteraction))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerUI))]
[RequireComponent(typeof(PlayerCamera))]
public class Player : MonoBehaviour
{
    [Header("Components")]
    public PlayerController Controller;
    public PlayerInventory Inventory;
    public PlayerInteraction Interaction;
    public PlayerUI UI;
    public PlayerCamera Camera;
    public Transform Head;

    [Header("Configuration")]
    public PlayerSettings Settings;

    public PlayerControls Input { get; private set; }
    public StateMachine<Player> StateMachine { get; private set; }
    public Dictionary<Type, State<Player>> States { get; private set; }

    public GroundState GroundState { get; private set; }
    public AirborneState AirborneState { get; private set; }
    public ClimbingState ClimbingState { get; private set; }

    private void Awake()
    {
        Controller.Initialize(this);
        Interaction.Initialize(this);
        Inventory.Initialize(this);
        UI.Initialize(this);

        Input = new PlayerControls();
        StateMachine = new StateMachine<Player>(this);
        GroundState = new GroundState(this, StateMachine);
        AirborneState = new AirborneState(this, StateMachine);
        ClimbingState = new ClimbingState(this, StateMachine);
    }

    private void Start()
    {
        StateMachine.Initialize(GroundState);
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
        StateMachine.LogicUpdate();

        HandlePause();
        HandleInput();
        
        Interaction.UpdateInteractionLogic();
    }
    
    private void FixedUpdate()
    {
        StateMachine.PhysicsUpdate();
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

    private void HandleInput() // FIXME: Remove this completly (Single Responsibility Principle)
    {
        if (Interaction == null || Inventory == null) return;

        bool interactPressed = Input.Player.Interact.WasPressedThisFrame();
        bool interactHeld = Input.Player.Interact.IsPressed();
        Interaction.SetInputs(interactPressed, interactHeld);

        bool primaryUse = Input.Player.Use.WasPressedThisFrame();
        bool secondaryUse = Input.Player.AltUse.WasPressedThisFrame();
        float scrollDelta = Input.Player.ScrollSlot.ReadValue<Vector2>().y;

        int slotToSelect = -1;
        if (Input.Player.SwitchSlot.WasPressedThisFrame())
        {
            var control = Input.Player.SwitchSlot.activeControl;
            if (control != null) slotToSelect = GetSlotFromControl(control);
        }
        Inventory.SetInputs(primaryUse, secondaryUse, scrollDelta, slotToSelect);
    }
    public T GetState<T>() where T : State<Player>
    {
        return (T)States[typeof(T)];
    }

    private int GetSlotFromControl(UnityEngine.InputSystem.InputControl control)
    {
        switch (control.name)
        {
            case "1": return 0;
            case "2": return 1;
            case "3": return 2;
            case "4": return 3;
            case "5": return 4;
            case "6": return 5;
            case "7": return 6;
            case "8": return 7;
            case "9": return 8;
            case "0": return 9;
            default: return -1;
        }
    }
}