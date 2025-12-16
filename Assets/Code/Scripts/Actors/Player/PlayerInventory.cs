using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("References")]
    public Player Player;
    public Transform EquipPoint;

    [Header("Settings")]
    public int MaxSlots = 5;

    private Dictionary<string, int> _backpack = new Dictionary<string, int>();
    private Equipable[] _slots;
    private int _currentSlotIndex = -1;

    public Equipable CurrentEquippedItem { get; private set; }

    public void Initialize(Player player)
    {
        Player = player;
        _slots = new Equipable[MaxSlots];
    }

    public void AddResource(string resourceID, int amount)
    {
        if (string.IsNullOrEmpty(resourceID)) return;

        if (_backpack.ContainsKey(resourceID))
        {
            _backpack[resourceID] += amount;
        }
        else
        {
            _backpack.Add(resourceID, amount);
        }

        Debug.Log($"[Inventory] Resource Added: {resourceID} (Total: {_backpack[resourceID]})");
        
        // Tu można dodać wywołanie UI update amunicji, jeśli jest widoczne
    }

    public int GetResourceCount(string resourceID)
    {
        if (_backpack.TryGetValue(resourceID, out int count))
        {
            return count;
        }
        return 0;
    }

    public bool TryConsumeResource(string resourceID, int amount)
    {
        if (GetResourceCount(resourceID) >= amount)
        {
            _backpack[resourceID] -= amount;
            return true;
        }
        return false;
    }

    public bool TryEquipItem(Equipable newItem)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = newItem;
                
                newItem.transform.SetParent(EquipPoint);
                newItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                
                if (CurrentEquippedItem == null)
                {
                    SelectSlot(i);
                }
                else
                {
                    newItem.gameObject.SetActive(false);
                }
                
                Debug.Log($"[Inventory] Equipped {newItem.ItemName} in slot {i}");
                return true;
            }
        }

        Debug.Log("[Inventory] No free slots!");
        return false;
    }

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Length) return;

        // Dezaktywuj obecny
        if (CurrentEquippedItem != null)
        {
            CurrentEquippedItem.OnUnequip();
        }

        _currentSlotIndex = slotIndex;
        CurrentEquippedItem = _slots[slotIndex];

        // Aktywuj nowy
        if (CurrentEquippedItem != null)
        {
            CurrentEquippedItem.OnEquip(this);
        }
    }

    // Input dla broni (LPM/PPM)
    public void HandleEquipmentInput(bool primary, bool secondary)
    {
        if (CurrentEquippedItem != null)
        {
            if (primary) CurrentEquippedItem.UsePrimary();
            if (secondary) CurrentEquippedItem.UseSecondary();
        }
    }

    public void CycleSlot(int direction)
    {
        if (_slots == null || _slots.Length == 0) return;

        // direction: > 0 (góra/poprzedni), < 0 (dół/następny)
        // Oblicz nowy indeks z zawijaniem (modulo)
        int newIndex = _currentSlotIndex + (direction > 0 ? -1 : 1);
        
        // Obsługa ujemnych indeksów i przekroczenia zakresu
        if (newIndex < 0) newIndex = _slots.Length - 1;
        if (newIndex >= _slots.Length) newIndex = 0;

        SelectSlot(newIndex);
    }

    public Equipable[] GetSlots()
    {
        return _slots;
    }
    
    public int GetCurrentSlotIndex()
    {
        return _currentSlotIndex;
    }
}
                