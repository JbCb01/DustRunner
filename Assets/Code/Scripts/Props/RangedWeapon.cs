using UnityEngine;

public class RangedWeapon : Equipable
{
    [Header("Weapon Stats")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private int clipSize = 6;
    [SerializeField] private int currentClip = 6;

    [Header("Recoil & Sway")]
    [SerializeField] private float recoilForce = 2f;
    
    [Header("Ammunition")]
    [SerializeField] private ItemData ammoType;
    private float _nextFireTime;
    
    public override string GetUIStatus()
    {
        if (Player.Inventory == null) return "0 / 0";
        int backpackAmmo = Player.Inventory.GetResourceCount(ammoType);
        return $"{currentClip} / {backpackAmmo}";
    }

    public override void UsePrimary()
    {
        if (Time.time < _nextFireTime) return;

        if (currentClip > 0)
        {
            Shoot();
            _nextFireTime = Time.time + fireRate;
            currentClip--;
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
        if (Player.Inventory == null) return;
        if (currentClip >= clipSize) return; // Pełny magazynek

        int ammoNeeded = clipSize - currentClip;
        int ammoAvailable = Player.Inventory.GetResourceCount(ammoType);

        if (ammoAvailable > 0)
        {
            int ammoToLoad = Mathf.Min(ammoNeeded, ammoAvailable);
            Player.Inventory.TryConsumeResource(ammoType, ammoToLoad);
            currentClip += ammoToLoad;
            
            Debug.Log("Reloaded.");
        }
        else
        {
            Debug.Log("No ammo in backpack!");
        }
    }

    private void Shoot()
{
    Transform camTransform = Player.Camera.Main.transform;
    Ray ray = new Ray(camTransform.position, camTransform.forward);

    if (Physics.Raycast(ray, out RaycastHit hit, range, hitLayers))
    {
        Debug.Log($"[Weapon] Hit: {hit.collider.name} on Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        
        if (damageable != null)
        {
            damageable.TakeDamage(damage, hit.point, hit.normal);
        }
        else 
        {
            Debug.LogWarning($"[Weapon] Object {hit.collider.name} has no IDamageable component!");
        }

    }
    else
    {
        Debug.Log("[Weapon] Raycast hit nothing.");
    }
    
    ApplyRecoil();
}

    private void ApplyRecoil()
    {
        transform.parent.localRotation *= Quaternion.Euler(-recoilForce, Random.Range(-recoilForce, recoilForce), 0);
    }
}