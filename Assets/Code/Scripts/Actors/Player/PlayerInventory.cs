using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("References")]
    public Player Player;
    public Transform EquipPoint;

    [Header("Settings")]
    [SerializeField] public int maxSlots = 3;
    [SerializeField] private float dropForce = 5f;

    // Plecak na surowce (słownik danych)
    private Dictionary<ItemData, int> _backpack = new Dictionary<ItemData, int>();

    public int CurrentSlotIndex { get; private set; } = 0;
    public Equipable[] Slots;

    public Equipable CurrentEquippedItem => Slots[CurrentSlotIndex];

    public void Initialize(Player player)
    {
        Player = player;
        Slots = new Equipable[maxSlots];
    }

    public void HandleLoot(ItemData item, int amount)
    {
        if (item == null) return;

        if (item is PickableData)
        {
            AddResource(item, amount);
        }
        else if (item is EquipableData equipableData)
        {
            SpawnAndEquipWeapon(equipableData);
        }
    }

    private void SpawnAndEquipWeapon(EquipableData data)
    {
        if (data.WorldPrefab == null)
        {
            Debug.LogError($"[Inventory] EquipableData '{data.DisplayName}' has no WorldPrefab assigned!");
            return;
        }

        GameObject weaponObj = Instantiate(data.WorldPrefab);
        Equipable newWeapon = weaponObj.GetComponent<Equipable>();

        if (newWeapon != null)
        {
            if (newWeapon.Data == null) newWeapon.Data = data;
            TryEquipItem(newWeapon);
        }
        else
        {
            Debug.LogError($"[Inventory] Prefab for '{data.DisplayName}' is missing Equipable component!");
            Destroy(weaponObj);
        }
    }

    public void TryEquipItem(Equipable newItem)
    {
        if (newItem == null) return;

        if (Slots[CurrentSlotIndex] != null)
        {
            DropItemFromSlot(CurrentSlotIndex);
        }

        Slots[CurrentSlotIndex] = newItem;
        
        newItem.transform.SetParent(EquipPoint);
        newItem.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        
        newItem.AssignOwner(Player); 
        newItem.OnEquip();
        
        Debug.Log($"[Inventory] Equipped: {newItem.name}");
    }

    public void DropCurrentItem()
    {
        if (CurrentEquippedItem == null) return;
        DropItemFromSlot(CurrentSlotIndex);
    }

    private void DropItemFromSlot(int index)
    {
        Equipable itemToDrop = Slots[index];
        if (itemToDrop == null) return;

        Slots[index] = null;

        // Wyliczanie kierunku wyrzutu (z kamery gracza)
        Vector3 dropDir = Player.Camera.Main.transform.forward;
        dropDir = (dropDir + Vector3.up * 0.15f).normalized;

        itemToDrop.Throw(dropDir, dropForce);
        
        Debug.Log($"[Inventory] Dropped: {itemToDrop.name}");
    }

    public void HandleEquipmentInput(bool primary, bool secondary, bool dropItem)
    {
        if (dropItem)
        {
            HandleDropInput();
            return;
        }
        if (CurrentEquippedItem != null)
        {
            if (primary) CurrentEquippedItem.UsePrimary();
            if (secondary) CurrentEquippedItem.UseSecondary();
        }
    }

    public void HandleDropInput()
    {
        // Sprawdzamy czy patrzymy na skrzynię
        Lootable targetContainer = Player.Interaction.CurrentInteractable as Lootable;

        if (CurrentEquippedItem == null) return;

        if (targetContainer != null)
        {
            targetContainer.AddItem(CurrentEquippedItem.Data);
            
            Equipable itemToRemove = CurrentEquippedItem;
            Slots[CurrentSlotIndex] = null;
            
            Destroy(itemToRemove.gameObject);
            
            Debug.Log("[Inventory] Stored item in container.");
        }
        else
        {
            DropCurrentItem();
        }
    }

    // --- ZARZĄDZANIE SLOTAMI ---

    public void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Slots.Length || slotIndex == CurrentSlotIndex) return;

        if (CurrentEquippedItem != null) CurrentEquippedItem.OnUnequip();

        CurrentSlotIndex = slotIndex;

        if (CurrentEquippedItem != null) CurrentEquippedItem.OnEquip();
    }

    public void CycleSlot(int direction)
    {
        int nextIndex = CurrentSlotIndex + (direction > 0 ? -1 : 1);
        
        if (nextIndex < 0) nextIndex = Slots.Length - 1;
        if (nextIndex >= Slots.Length) nextIndex = 0;

        SelectSlot(nextIndex);
    }


    public void AddResource(ItemData item, int amount)
    {
        if (item == null) return;
        
        if (_backpack.ContainsKey(item)) 
        {
            _backpack[item] += amount;
        }
        else 
        {
            _backpack.Add(item, amount);
        }

        Debug.Log($"[Inventory] Added {amount}x {item.DisplayName}. Total: {_backpack[item]}");
    }

    public int GetResourceCount(ItemData item)
    {
        if (item == null) return 0;
        return _backpack.TryGetValue(item, out int count) ? count : 0;
    }

    public bool TryConsumeResource(ItemData item, int amount)
    {
        if (GetResourceCount(item) >= amount)
        {
            _backpack[item] -= amount;
            return true;
        }
        return false;
    }
}