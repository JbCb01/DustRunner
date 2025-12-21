using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public Player Player;

    [Header("Settings")]
    public float InteractionRange = 3.0f;
    public LayerMask InteractionLayers;

    public IInteractable CurrentInteractable { get; private set; }
    public IScrollable CurrentScrollable { get; private set; }

    public void Initialize(Player player)
    {
        Player = player;
    }

    public void UpdateInteractionScan()
    {
        CurrentInteractable = null;
        CurrentScrollable = null;

        if (Player.Camera == null) return;

        Transform camTransform = Player.Camera.Main.transform;
        Ray ray = new Ray(camTransform.position, camTransform.forward);

        // Raycast
        if (Physics.Raycast(ray, out RaycastHit hit, InteractionRange, InteractionLayers, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                CurrentInteractable = interactable;
            }
            else if (hit.collider.attachedRigidbody != null && 
                     hit.collider.attachedRigidbody.TryGetComponent<IInteractable>(out var rbInteractable))
            {
                CurrentInteractable = rbInteractable;
            }

            if (CurrentInteractable != null && CurrentInteractable is IScrollable scrollable)
            {
                CurrentScrollable = scrollable;
            }
        }
    }

    public void HandleInteractionInput(bool interactPressed, float scrollValue)
    {
        if (CurrentScrollable != null && Mathf.Abs(scrollValue) > 0.05f)
        {
            CurrentScrollable.OnScroll(scrollValue);
            return; // Scrollowanie lootem blokuje zmianę broni
        }

        if (interactPressed && CurrentInteractable != null && CurrentInteractable.CanInteract)
        {
            CurrentInteractable.Interact(Player);
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
}