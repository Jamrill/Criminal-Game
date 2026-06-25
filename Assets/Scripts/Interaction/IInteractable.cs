namespace JuegoCriminal.Interaction
{
    public interface IInteractable
    {
        bool CanShowPrompt();
        bool CanInteract();

        void Interact();

        string GetInteractionText();
    }
}