using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Equipable : MonoBehaviour, IInteractable
{
    [Header("Settings")]
    public EquipableData Data;


    private Rigidbody _rb;
    private Collider[] _colliders;
    private Renderer[] _worldRenderers;
    private GameObject _currentViewModel;
    private bool _hasOwner;

    public bool CanInteract { get; } = true;
    public Player Player { get; private set; }
    
    public virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>();
        _worldRenderers = GetComponentsInChildren<Renderer>();
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
            ToggleWorldVisuals(true);
            DestroyViewModel();
        }
    }

    public void OnEquip()
    {
        if (!_hasOwner) return;
        gameObject.SetActive(true);

        if (Data != null && Data.ViewModelPrefab != null)
        {
            ToggleWorldVisuals(false);
            CreateViewModel();
        }
    }
    public void OnUnequip()
    {
        gameObject.SetActive(false);
        DestroyViewModel();
        ToggleWorldVisuals(true);
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
    public virtual string GetUIStatus() { return null; }

    public virtual void OnViewModelCreated(GameObject viewModel) { }

    private void CreateViewModel()
    {
        if (_currentViewModel != null) return;

        _currentViewModel = Instantiate(Data.ViewModelPrefab, transform);
        _currentViewModel.transform.localPosition = Vector3.zero;
        _currentViewModel.transform.localRotation = Quaternion.identity;

        OnViewModelCreated(_currentViewModel);
    }

    private void DestroyViewModel()
    {
        if (_currentViewModel != null)
        {
            Destroy(_currentViewModel);
            _currentViewModel = null;
        }
    }

    private void ToggleWorldVisuals(bool state)
    {
        if (_worldRenderers == null) return;
        foreach (var rend in _worldRenderers)
        {
            rend.enabled = state;
        }
    }
}