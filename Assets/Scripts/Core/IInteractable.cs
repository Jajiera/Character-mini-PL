using UnityEngine;

namespace Scripts.Core
{
    public interface IInteractable
    {
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
        string GetInteractionPrompt();
        void OnFocusGained();
        void OnFocusLost();
    }
}
