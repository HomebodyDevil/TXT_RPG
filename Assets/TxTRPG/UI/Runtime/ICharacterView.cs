namespace TxTRPG.UI
{
    public interface ICharacterView
    {
        bool IsVisible { get; }

        void Show(in CharacterPresentation presentation);
        void UpdatePresentation(in CharacterPresentation presentation);
        void PlayAnimation(string animationId);
        void PlayEffect(string effectId);
        void Hide();
        void Clear();
    }
}
