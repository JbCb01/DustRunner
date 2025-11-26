using UnityEngine;
using UnityEngine.InputSystem;

public abstract class PlayerActiveState : State<Player>
{
    protected PlayerActiveState(Player player, StateMachine<Player> stateMachine) : base(player, stateMachine) { }

    public override void LogicUpdate()
    {
        base.LogicUpdate();

        if (Owner.Input.Player.Pause.WasPressedThisFrame())
        {
            StateMachine.ChangeState(Owner.MenuState);
            return;
        }

        HandleInteractionInput();
        HandleInventoryInput();
    }

    private void HandleInteractionInput()
    {
        bool interactPressed = Owner.Input.Player.Interact.WasPressedThisFrame();
        bool interactHeld = Owner.Input.Player.Interact.IsPressed();
        
        Owner.Interaction.SetInputs(interactPressed, interactHeld);
    }

    private void HandleInventoryInput()
    {
        bool primaryUse = Owner.Input.Player.Use.WasPressedThisFrame();
        bool secondaryUse = Owner.Input.Player.AltUse.WasPressedThisFrame();
        float scrollDelta = Owner.Input.Player.ScrollSlot.ReadValue<Vector2>().y;

        int slotToSelect = -1;
        if (Owner.Input.Player.SwitchSlot.WasPressedThisFrame())
        {
            var control = Owner.Input.Player.SwitchSlot.activeControl;
            if (control != null) slotToSelect = GetSlotFromControl(control);
        }

        Owner.Inventory.SetInputs(primaryUse, secondaryUse, scrollDelta, slotToSelect);
    }

    protected int GetSlotFromControl(InputControl control)
    {
        if (int.TryParse(control.name, out int number))
        {
            return number - 1; 
        }
        return -1;
    }
}