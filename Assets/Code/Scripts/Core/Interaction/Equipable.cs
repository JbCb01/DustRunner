using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Equipable : MonoBehaviour, IInteractable
{
    [Header("Equipable Settings")]
    public bool CanInteract => true;
    public EquipableData Data;

    private Rigidbody _rb;
    private Collider[] _colliders;
    private bool _hasOwner;

    public Player Player { get; private set; }
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
    }
    public void AssignOwner(Player player)
    {
        Player = player;
        _hasOwner = true;
        SetPhysicsState(true);
    }
    public void Interact(Player player)
    {
        player.Inventory.TryEquipItem(this);
    }

    public void SetPhysicsState(bool isEquipped)
    {
        _rb.isKinematic = isEquipped;
        _rb.interpolation = isEquipped ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;

        foreach (var col in _colliders)
        {
            col.enabled = !isEquipped;
        }
        
        if (!isEquipped)
        {
            transform.SetParent(null);
        }
    }

    public void OnEquip()
    {
        if (!_hasOwner) return;
        gameObject.SetActive(true);
    }
    public void OnUnequip()
    {
        gameObject.SetActive(false);
    }

    public void Throw(Vector3 direction, float force)
    {
        SetPhysicsState(false);
        transform.SetParent(null);
        _rb.AddForce(direction * force, ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * force, ForceMode.Impulse);
        
        Player = null;
        _hasOwner = false;
    }

    public virtual void UsePrimary() { }
    public virtual void UseSecondary() { }
    public virtual string GetUIStatus() 
    { 
        return null; 
    }
}