using UnityEngine;

public class Equipable : MonoBehaviour
{
    public PlayerInventory Inventory { get; private set; }

    public void OnEquip(PlayerInventory inventory)
    {
        Inventory = inventory;
        gameObject.SetActive(true);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    public void OnUnequip()
    {
        Inventory = null;
        gameObject.SetActive(false);
    }

    public void UsePrimary() { }
    public void UseSecondary() { }
}