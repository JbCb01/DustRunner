using UnityEngine;

public class Equipable : MonoBehaviour, IInteractable
{
    [Header("Equipable Settings")]
    public string ItemName = "Equipable Item";
    
    public PlayerInventory Inventory { get; private set; }

    // --- IInteractable Implementation (Podnoszenie z ziemi) ---
    public bool CanInteract => true; // Można dodać warunek: && Inventory == null
    public string InteractionName => $"Equip {ItemName}";

    public void Interact(Player player)
    {
        Debug.Log($"[Equipable] Picking up weapon: {ItemName}");
        
        if (player.Inventory.TryEquipItem(this))
        {
            
        }
    }

    // --- Istniejąca logika obsługi przedmiotu (zostawiamy bez zmian logicznych) ---
    public void OnEquip(PlayerInventory inventory)
    {
        Inventory = inventory;
        gameObject.SetActive(true);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public void OnUnequip()
    {
        gameObject.SetActive(false);
    }

    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
    public virtual string GetAmmoStatus() 
    { 
        return null; 
    }
}