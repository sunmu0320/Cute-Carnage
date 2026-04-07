using UnityEngine;

public interface IInteractable
{
    Transform GetUIAnchor();
    Vector3 GetInteractPosition();
    bool CanInteract(PlayerInteractor interactor);
    InteractablePromptData GetInteractionPromptData(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
