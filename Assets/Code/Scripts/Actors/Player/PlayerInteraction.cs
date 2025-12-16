// Assets/Code/Scripts/Actors/Player/PlayerInteraction.cs
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public Player Player;

    [Header("Settings")]
    public float InteractionRange = 3.0f;
    public LayerMask InteractionLayers; // Upewnij się, że ustawisz to w Unity!

    // Public properties for UI to read
    public IInteractable CurrentInteractable { get; private set; }
    public IScrollable CurrentScrollable { get; private set; }

    public void Initialize(Player player)
    {
        Player = player;
    }

    // Wywoływane z Player.cs w Update()
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
            // 1. Sprawdź komponent na uderzonym obiekcie
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                CurrentInteractable = interactable;
            }
            // 2. Sprawdź Rigidbody (dla obiektów złożonych, np. collider dziecka, skrypt na rodzicu)
            else if (hit.collider.attachedRigidbody != null && 
                     hit.collider.attachedRigidbody.TryGetComponent<IInteractable>(out var rbInteractable))
            {
                CurrentInteractable = rbInteractable;
            }

            // 3. Jeśli mamy interakcję, sprawdź czy obsługuje Scroll (Lootable)
            if (CurrentInteractable != null && CurrentInteractable is IScrollable scrollable)
            {
                CurrentScrollable = scrollable;
            }
        }
    }

    // Wywoływane z Player.cs przy Inputcie (Action Context)
    public void HandleInteractionInput(bool interactPressed, float scrollValue)
    {
        // 1. Scroll ma priorytet jeśli patrzymy na kontener
        if (CurrentScrollable != null && Mathf.Abs(scrollValue) > 0.05f)
        {
            CurrentScrollable.OnScroll(scrollValue);
            return; // Scrollowanie lootem blokuje zmianę broni
        }

        // 2. Interakcja
        if (interactPressed && CurrentInteractable != null && CurrentInteractable.CanInteract)
        {
            CurrentInteractable.Interact(Player);
        }
        
        // Pushable (zostawiamy fizykę w spokoju, jest obsługiwana przez CharacterController collisions)
    }
    
    // Metoda dla Pushable (zgodnie z Twoim starym kodem, jeśli chcesz zachować pchanie "ciałem")
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