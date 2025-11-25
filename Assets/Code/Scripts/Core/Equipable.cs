using UnityEngine;

public abstract class Equipable : MonoBehaviour
{
    public PlayerInventory Inventory { get; private set; }

    public virtual void OnEquip(PlayerInventory inventory)
    {
        Inventory = inventory;
        gameObject.SetActive(true);
    }

    public virtual void OnUnequip()
    {
        Inventory = null;
        gameObject.SetActive(false);
    }

    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
}