using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public Player Player;

    [Header("Settings")]
    public float MaxPushAngle = 45f;
    public float InteractionRange = 2f;
    public Transform HoldPoint;
    public LayerMask GrabLayers;
    public LayerMask InteractionLayers;
    public LayerMask EquipableLayers;

    public Usable CurrentUsable { get; private set; }
    public Grabbable CurrentGrabbedObject { get; private set; }
    
    public void Initialize(Player player)
    {
        Player = player;
    }
    public void UpdateInteractionLogic()
    {
        if (CurrentGrabbedObject != null)
        {
            CurrentGrabbedObject.UpdateGrab(HoldPoint.position, Time.deltaTime);
            return; // Don't detect new things while holding something
        }

        CurrentUsable = null;
        if (Player.Camera == null) return;

        Ray ray = new(Player.Camera.Main.transform.position, Player.Camera.Main.transform.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, InteractionRange, InteractionLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.TryGetComponent<Usable>(out var usable))
            {
                if (usable.IsUsable) CurrentUsable = usable;
            }
        }

        // Scan for equipable items
        if (Physics.Raycast(ray, out RaycastHit equipHit, InteractionRange, EquipableLayers, QueryTriggerInteraction.Collide))
        {
            if (equipHit.collider.TryGetComponent<Equipable>(out var equipable))
            {
                // We can show a UI prompt here if we want
            }
        }
    }

    public void TriggerInteraction(Vector3 playerPosition)
    {
        if (CurrentUsable != null && CurrentUsable.IsUsable)
        {
            CurrentUsable.Interact(playerPosition);
        }
    }

    public bool ProcessPhysicalInteraction(Collider hitCollider, Vector3 characterVelocity)
    {
        if (hitCollider.attachedRigidbody == null) return false;
        if (!hitCollider.TryGetComponent<Pushable>(out var pushable)) return false;

        Vector3 pushDir = new Vector3(characterVelocity.x, 0f, characterVelocity.z);
        if (pushDir.sqrMagnitude < 0.1f) return false;
        
        pushDir.Normalize();
        pushable.Push(pushDir);
        
        return true;
    }

    public void SetInputs(bool interactPressed, bool interactHeld)
    {
        if (CurrentGrabbedObject != null && !interactHeld)
        {
            DropObject();
            return;
        }

        if (interactPressed && CurrentGrabbedObject == null)
        {
            if (TryGrabObject()) return;
            if (TryEquipObject()) return;
            
            if (CurrentUsable != null)
            {
                CurrentUsable.Interact(transform.position);
            }
        }
    }

    private bool TryGrabObject()
    {
        Ray ray = new(Player.Camera.Main.transform.position, Player.Camera.Main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, InteractionRange, GrabLayers))
        {
            if (hit.collider.attachedRigidbody != null && 
                hit.collider.attachedRigidbody.TryGetComponent<Grabbable>(out var grabbable))
            {
                Debug.Log("Grabbing object: " + grabbable.name);
                CurrentGrabbedObject = grabbable;
                CurrentGrabbedObject.BeginGrab(hit.point); // Grab EXACTLY where we clicked
                return true;
            }
        }
        return false;
    }

    private bool TryEquipObject()
    {
        Ray ray = new(Player.Camera.Main.transform.position, Player.Camera.Main.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, InteractionRange, EquipableLayers))
        {
            if (hit.collider.TryGetComponent<Equipable>(out var equipable))
            {
                if (Player.Inventory.AddItem(equipable))
                {
                    Debug.Log("Equipped object: " + equipable.name);
                    return true;
                }
            }
        }
        return false;
    }

    private void DropObject()
    {
        if (CurrentGrabbedObject != null)
        {
            CurrentGrabbedObject.EndGrab();
            CurrentGrabbedObject = null;
        }
    }


}