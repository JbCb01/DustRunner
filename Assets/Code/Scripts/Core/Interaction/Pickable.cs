using UnityEngine;

public class Pickable : MonoBehaviour, IInteractable
{
    [Header("Resource Data")]
    [Tooltip("Unique ID for the inventory dictionary (e.g., 'Ammo_9mm')")]
    public string ResourceID;
    
    public string DisplayName = "Resource";
    public int Quantity = 1;
    public int Amount = 1;
    public int MaxStack = 100; // Limit per slot in virtual bag
    public bool DestroyOnPickup = true;
    public bool CanInteract => true;
    public string InteractionName => $"Pick up {DisplayName}";

    public void Interact(Player player)
    {
        Debug.Log($"[Pickable] Adding {Amount} {ResourceID} to player inventory.");
        
        player.Inventory.AddResource(ResourceID, Quantity);
        if (DestroyOnPickup)
        {
            Destroy(gameObject);
        }
    }
}