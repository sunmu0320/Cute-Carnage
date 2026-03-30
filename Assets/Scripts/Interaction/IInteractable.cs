public interface IInteractable
{
    string GetInteractionPrompt();
    bool CanInteract(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
