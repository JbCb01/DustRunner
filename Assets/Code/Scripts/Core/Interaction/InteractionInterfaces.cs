public interface IInteractable
{
    bool CanInteract { get; }
    void Interact(Player player);
}

public interface IScrollable
{
    void OnScroll(float scrollDelta);
    string GetCurrentSelectionInfo(); 
}