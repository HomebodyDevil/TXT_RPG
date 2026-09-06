using System;
using TxTRPG.Gameplay.Characters;

namespace TxTRPG.UI
{
    public sealed class CharacterPresentationFactory
    {
        public CharacterPresentation Create(
            CharacterRuntimeState state,
            string appearanceId = "",
            string poseId = "",
            string expressionId = "",
            string animationId = "",
            bool mirrored = false)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new CharacterPresentation(
                state.CharacterDefinitionId,
                appearanceId,
                poseId,
                expressionId,
                animationId,
                mirrored);
        }
    }
}
