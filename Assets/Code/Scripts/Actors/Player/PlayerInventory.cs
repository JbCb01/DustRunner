using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public Player Player;

    [Header("Settings")]
    public int MaxSlots = 10;
    public Transform EquipPoint;

    private Equipable[] _slots;
    private int _currentSlotIndex = -1;

    public Equipable CurrentEquippedItem { get; private set; }

    public void Initialize(Player player)
    {
        Player = player;
        _slots = new Equipable[MaxSlots];
    }

    public void SetInputs(bool primaryUse, bool secondaryUse, float scrollDelta, int slotToSelect)
    {
        if (CurrentEquippedItem != null)
        {
            if (primaryUse)
            {
                CurrentEquippedItem.UsePrimary();
            }
            if (secondaryUse)
            {
                CurrentEquippedItem.UseSecondary();
            }
        }

        if (scrollDelta > 0)
        {
            SelectNextSlot();
        }
        else if (scrollDelta < 0)
        {
            SelectPreviousSlot();
        }

        if (slotToSelect != -1)
        {
            SelectSlot(slotToSelect);
        }
    }

    public bool AddItem(Equipable item)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = item;
                item.transform.SetParent(EquipPoint);
                item.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                item.gameObject.SetActive(false);
                return true;
            }
        }
        return false; // Inventory full
    }

    public void SelectSlot(int slotIndex)
    {
        if (CurrentEquippedItem != null)
        {
            Debug.Log("You must drop your current item before switching.");
            return;
        }

        if (slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex] == null)
        {
            _currentSlotIndex = -1;
            CurrentEquippedItem = null;
            return;
        }

        _currentSlotIndex = slotIndex;
        CurrentEquippedItem = _slots[_currentSlotIndex];
        CurrentEquippedItem.OnEquip(this);
    }

    public void DropCurrentItem()
    {
        if (CurrentEquippedItem != null)
        {
            CurrentEquippedItem.OnUnequip();
            _slots[_currentSlotIndex] = null;
            CurrentEquippedItem = null;
            _currentSlotIndex = -1;
        }
    }

    private void SelectNextSlot()
    {
        SelectSlot((_currentSlotIndex + 1) % MaxSlots);
    }

    private void SelectPreviousSlot()
    {
        int newIndex = _currentSlotIndex - 1;
        if (newIndex < 0) newIndex = MaxSlots - 1;
        SelectSlot(newIndex);
    }
}
