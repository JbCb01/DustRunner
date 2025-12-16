using UnityEngine;

public class RangedWeapon : Equipable
{
    [Header("Weapon Stats")]
    public string AmmoTypeID = "Ammo_9mm"; // ID musi pasować do Pickable
    public int ClipSize = 6;
    public int CurrentClip = 6;

    public override string GetAmmoStatus()
    {
        if (Inventory == null) return "0 / 0";
        int backpackAmmo = Inventory.GetResourceCount(AmmoTypeID);
        return $"{CurrentClip} / {backpackAmmo}";
    }

    public override void UsePrimary()
    {
        if (CurrentClip > 0)
        {
            Debug.Log($"Bang! ({ItemName})");
            CurrentClip--;
        }
        else
        {
            Debug.Log("*Click* (Empty)");
        }
    }

    public override void UseSecondary()
    {
        Reload();
    }

    private void Reload()
    {
        if (Inventory == null) return;
        if (CurrentClip >= ClipSize) return; // Pełny magazynek

        int ammoNeeded = ClipSize - CurrentClip;
        int ammoAvailable = Inventory.GetResourceCount(AmmoTypeID);

        if (ammoAvailable > 0)
        {
            int ammoToLoad = Mathf.Min(ammoNeeded, ammoAvailable);
            Inventory.TryConsumeResource(AmmoTypeID, ammoToLoad);
            CurrentClip += ammoToLoad;
            
            Debug.Log("Reloaded.");
        }
        else
        {
            Debug.Log("No ammo in backpack!");
        }
    }
}