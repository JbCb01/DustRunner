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
    public MenuState MenuState { get; private set; }

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
        MenuState = new MenuState(this, StateMachine);
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
}