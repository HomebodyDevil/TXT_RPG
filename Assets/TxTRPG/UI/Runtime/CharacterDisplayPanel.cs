using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class CharacterDisplayPanel : MonoBehaviour
    {
        [SerializeField] private CharacterViewBase activeView;
        [SerializeField] private PanelBackgroundRenderer backgroundRenderer;

        public bool IsVisible => activeView != null && activeView.IsVisible;
        public PanelBackgroundRenderer BackgroundRenderer => backgroundRenderer;

        public void ShowCharacter(in CharacterPresentation presentation)
        {
            if (activeView == null)
            {
                Debug.LogWarning("CharacterDisplayPanel has no active view.", this);
                return;
            }

            activeView.Show(presentation);
        }

        public void ShowCharacterImmediately(in CharacterPresentation presentation)
        {
            if (activeView == null)
            {
                Debug.LogWarning("CharacterDisplayPanel has no active view.", this);
                return;
            }
            activeView.ShowImmediately(presentation);
        }

        public void UpdateCharacter(in CharacterPresentation presentation)
        {
            activeView?.UpdatePresentation(presentation);
        }

        public void PlayAnimation(string animationId)
        {
            activeView?.PlayAnimation(animationId);
        }

        public void PlayEffect(string effectId)
        {
            activeView?.PlayEffect(effectId);
        }

        public void HideCharacter()
        {
            activeView?.Hide();
        }

        public void Clear()
        {
            activeView?.Clear();
        }

        public void ApplyBackground(PanelBackgroundStyle style) => backgroundRenderer?.ApplyStyle(style);

        public void ChangeBackground(PanelBackgroundStyle style, float duration = -1f) =>
            backgroundRenderer?.Change(style, duration);

        public void ClearBackground() => backgroundRenderer?.Clear();

        public void SetBackgroundEffectsEnabled(bool enabled) =>
            backgroundRenderer?.SetEffectsEnabled(enabled);

        public void SetBackgroundAssetProvider(IAssetProvider provider) =>
            backgroundRenderer?.SetAssetProvider(provider);

        public void SetView(CharacterViewBase view, bool clearPrevious = true)
        {
            if (activeView == view)
            {
                return;
            }

            if (clearPrevious)
            {
                activeView?.Clear();
            }

            activeView = view;
        }
    }
}
