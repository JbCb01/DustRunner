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

        bool interactPressed = Owner.Input.Player.Interact.WasPressedThisFrame();
        float scrollValue = Owner.Input.Player.ScrollSlot.ReadValue<Vector2>().y;
        bool isLooting = Owner.Interaction.CurrentScrollable != null;
        if(isLooting)
        {
            Owner.Interaction.HandleInteractionInput(interactPressed, scrollValue);
        }
        else
        {
            Owner.Interaction.HandleInteractionInput(interactPressed, 0f);
            if (Mathf.Abs(scrollValue) > 0.01f)
            {
                int direction = scrollValue > 0 ? 1 : -1;
                Owner.Inventory.CycleSlot(direction);
            }
        }

        HandleCombatInput();
        HandleSlotSelectionInput();
    }

    private void HandleCombatInput()
    {
        bool primaryUse = Owner.Input.Player.Use.WasPressedThisFrame(); // Lub IsPressed() dla ognia ciągłego
        bool secondaryUse = Owner.Input.Player.AltUse.WasPressedThisFrame();
        bool dropItem = Owner.Input.Player.Drop.WasPressedThisFrame();

        // Przekazujemy do Inventory, które przekaże do trzymanego Equipable
        Owner.Inventory.HandleEquipmentInput(primaryUse, secondaryUse, dropItem);
    }

    private void HandleSlotSelectionInput()
    {
        // Twoja oryginalna logika dla klawiszy 1-9 (zachowana)
        if (Owner.Input.Player.SwitchSlot.WasPressedThisFrame())
        {
            var control = Owner.Input.Player.SwitchSlot.activeControl;
            if (control != null)
            {
                int slotIndex = GetSlotFromControl(control);
                if (slotIndex != -1)
                {
                    Owner.Inventory.SelectSlot(slotIndex);
                }
            }
        }
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