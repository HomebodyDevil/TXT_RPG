using UnityEngine;

namespace TxTRPG.UI
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Character Display Demo Data")]
    public sealed class CharacterDisplayPanelDemoData : ScriptableObject
    {
        [SerializeField] private CharacterAppearanceDefinition appearanceDefinition;
        [SerializeField] private string characterId = "demo-character";
        [SerializeField] private string appearanceId = string.Empty;
        [SerializeField] private string poseId = string.Empty;
        [SerializeField] private string expressionId = string.Empty;
        [SerializeField] private string animationId = string.Empty;
        [SerializeField] private bool mirrored;

        public CharacterAppearanceDefinition AppearanceDefinition => appearanceDefinition;

        public CharacterPresentation ToPresentation()
        {
            return new CharacterPresentation(
                characterId,
                appearanceId,
                poseId,
                expressionId,
                animationId,
                mirrored);
        }
    }
}
