using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class CharacterDisplayPanel : MonoBehaviour
    {
        [SerializeField] private CharacterViewBase activeView;

        public bool IsVisible => activeView != null && activeView.IsVisible;

        public void ShowCharacter(in CharacterPresentation presentation)
        {
            if (activeView == null)
            {
                Debug.LogWarning("CharacterDisplayPanel has no active view.", this);
                return;
            }

            activeView.Show(presentation);
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
