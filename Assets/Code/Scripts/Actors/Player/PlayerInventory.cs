using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public Player Player;

    [Header("Settings")]
    public int MaxSlots = 4;
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

        // 2. Handle Switching
        if (slotToSelect != -1)
        {
            SelectSlot(slotToSelect);
        }
        else if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            if (scrollDelta > 0) SelectNextSlot();
            else SelectPreviousSlot();
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
                
                // If we picked up an item and hold nothing, equip it immediately
                if (CurrentEquippedItem == null)
                {
                    SelectSlot(i);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
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
            
            // Logic to spawn physical object in world would go here
            // e.g. Instantiate(CurrentEquippedItem.DropPrefab, ...)
            
            // For now, just destroy/remove logic
            Destroy(CurrentEquippedItem.gameObject); // Or Object Pooling return
            
            _slots[_currentSlotIndex] = null;
            CurrentEquippedItem = null;
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
