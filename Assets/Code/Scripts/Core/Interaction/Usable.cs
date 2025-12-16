using UnityEngine;
using UnityEngine.Events;

public class Usable : MonoBehaviour, IInteractable
{
    [Header("Configuration")]
    [SerializeField] private string _interactionName = "Use";
    [SerializeField] private bool _isUsable = true;

    [Header("Events")]
    public UnityEvent<Player> OnUsed;

    // --- Interface ---
    public bool CanInteract => _isUsable;
    public string InteractionName => _interactionName;

    public void Interact(Player player)
    {
        if (!_isUsable) return;
        
        Debug.Log($"[Usable] Interacted with: {name}");
        OnUsed?.Invoke(player);
    }

    public void SetUsable(bool state) => _isUsable = state;
}