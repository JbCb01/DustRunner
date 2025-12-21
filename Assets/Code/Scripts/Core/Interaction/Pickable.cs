using UnityEngine;

public class Pickable : MonoBehaviour, IInteractable
{
    [Header("Resource Data")]
    [Tooltip("Reference to the item data for this pickable resource")]
    public PickableData Data;
    
    public string DisplayName = "Resource";
    public int Quantity = 1;
    public int Amount = 1;
    public int MaxStack = 100; // Limit per slot in virtual bag
    public bool DestroyOnPickup = true;
    public bool CanInteract => true;

    public void Interact(Player player)
    {
        Debug.Log($"[Pickable] Adding {Amount} {DisplayName} to player inventory.");

        player.Inventory.AddResource(Data, Quantity);
        if (DestroyOnPickup)
        {
            Destroy(gameObject);
        }
    }
}