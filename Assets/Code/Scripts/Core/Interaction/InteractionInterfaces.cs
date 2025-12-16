using UnityEngine;

// Podstawa dla WSZYSTKIEGO co reaguje na "E"
public interface IInteractable
{
    bool CanInteract { get; }
    void Interact(Player player);
    string InteractionName { get; } 
}

public interface IScrollable
{
    void OnScroll(float scrollDelta);
    string GetCurrentSelectionInfo(); 
}