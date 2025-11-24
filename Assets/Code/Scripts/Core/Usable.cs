using UnityEngine;
using UnityEngine.Events;

public class Usable : MonoBehaviour
{
    [Header("Interaction Settings")]
    public bool IsUsable = true;
    public string PromptText = "Interact";

    // Event that other scripts (Door, LightSwitch) can listen to.
    // We pass Vector3 to know WHERE the player is standing.
    [Space]
    public UnityEvent<Vector3> OnInteract;

    public void Interact(Vector3 interactorPosition)
    {
        if (!IsUsable) return;
        OnInteract?.Invoke(interactorPosition);
    }
}