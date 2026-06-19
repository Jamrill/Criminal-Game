namespace JuegoCriminal.Interaction
{
    public interface IInteractable
    {
        bool CanInteract();
        void Interact();

        string GetInteractionText();
    }
}