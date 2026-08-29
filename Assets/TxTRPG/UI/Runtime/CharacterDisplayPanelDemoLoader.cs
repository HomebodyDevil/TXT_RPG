using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class CharacterDisplayPanelDemoLoader : MonoBehaviour
    {
        [SerializeField] private CharacterDisplayPanel target;
        [SerializeField] private Character2DView characterView;
        [SerializeField] private CharacterDisplayPanelDemoData data;

        private void Start()
        {
            if (target == null || characterView == null || data == null)
            {
                Debug.LogWarning("Character display demo references are incomplete.", this);
                return;
            }

            characterView.SetAppearanceDefinitions(new[] { data.AppearanceDefinition });
            target.ShowCharacter(data.ToPresentation());
        }
    }
}
