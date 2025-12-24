using UnityEngine;

public class RangedWeapon : Equipable
{
    [Header("Weapon Stats")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 100f;
    [SerializeField] private float fireRate = 0.5f;
    [SerializeField] private LayerMask hitLayers;
    [SerializeField] private int clipSize = 6;
    [SerializeField] private int currentClip = 6;

    [Header("Effects & Recoil")]
    [Tooltip("X: Vertical Climb, Y: Horizontal Jitter, Z: Screen Shake")]
    [SerializeField] private Vector3 rotationRecoil = new Vector3(5f, 2f, 1f);
    
    [Tooltip("X/Y: Side Shake, Z: Backward Kick (Shoulder impact)")]
    [SerializeField] private Vector3 positionRecoil = new Vector3(0.1f, 0.1f, 0.2f);
    
    [SerializeField] private float randomness = 0.5f;
    [SerializeField] private float impactForce = 5f;

    [Header("Ammunition")]
    [SerializeField] private ItemData ammoType;

    [Header("VFX")]
    [SerializeField] private ParticleSystem defaultMuzzleFlash;

    private ParticleSystem _currentMuzzleFlash;
    private float _nextFireTime;

    public override void Awake()
    {
        base.Awake();
        _currentMuzzleFlash = defaultMuzzleFlash;
    }

    public override void OnViewModelCreated(GameObject viewModel)
    {
        var flashTransform = viewModel.transform.Find("MuzzleFlash");
        if (flashTransform != null)
        {
            _currentMuzzleFlash = flashTransform.GetComponent<ParticleSystem>();
        }
        else
        {
            // Opcjonalnie: Szukaj w głąb (recursive) lub użyj GetComponentInChildren
            _currentMuzzleFlash = viewModel.GetComponentInChildren<ParticleSystem>();
        }
    }
    
    public override string GetUIStatus()
    {
        if (Player == null ||Player.Inventory == null) return "0 / 0";
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
        Reload(); // TODO: Zmienić na celowanie, dodać przeładowanie pod R
    }

    private void Reload()
    {
        if (Player == null || Player.Inventory == null) return;
        if (currentClip >= clipSize) return;

        int ammoNeeded = clipSize - currentClip;
        int ammoAvailable = Player.Inventory.GetResourceCount(ammoType);

        if (ammoAvailable > 0)
        {
            int ammoToLoad = Mathf.Min(ammoNeeded, ammoAvailable);
            Player.Inventory.TryConsumeResource(ammoType, ammoToLoad);
            currentClip += ammoToLoad;
        }
    }

    private void Shoot()
    {
        // 1. Visuals (Muzzle Flash)
        if (_currentMuzzleFlash != null) 
        {
            _currentMuzzleFlash.Play();
        }

        // 2. Camera Recoil (Juice)
        if (Player != null && Player.Camera != null)
        {
            // Przekazujemy parametry z inspektora broni
            // Zakładam, że w RangedWeapon masz pola: rotationRecoil, positionRecoil, randomness
            // Jeśli nie, dodaj je wg przykładu z poprzedniej rozmowy.
            
            Player.Camera.AddRecoil(
                rotationRecoil, // Rotation Kick (możesz zastąpić zmienną)
                positionRecoil,    // Position Kick
                randomness                                // Randomness
            );
        }

        // 3. Raycast logic
        Transform camTransform = Player.Camera.Main.transform;
        Ray ray = new Ray(camTransform.position, camTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitLayers))
        {
            Debug.Log($"[Weapon] Hit: {hit.collider.name}");

            // Handle Damage
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal);
            }

            // Handle Physics Push (Juice)
            // Szukamy Pushable (Twój komponent) lub zwykłego Rigidbody
            Pushable pushable = hit.collider.GetComponentInParent<Pushable>();
            if (pushable != null)
            {
                // Używamy dedykowanej metody Hit z Pushable.cs (jeśli ją dodałeś)
                pushable.Hit(hit.point, ray.direction, impactForce);
            }
            else if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                // Fallback dla zwykłych obiektów bez Pushable.cs
                hit.rigidbody.AddForceAtPosition(ray.direction * impactForce, hit.point, ForceMode.Impulse);
            }

            // TODO: Spawn Impact VFX at hit.point
        }
    }
}