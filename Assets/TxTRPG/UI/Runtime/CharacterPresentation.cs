namespace TxTRPG.UI
{
    public readonly struct CharacterPresentation
    {
        public CharacterPresentation(
            string characterId,
            string appearanceId = "",
            string poseId = "",
            string expressionId = "",
            string animationId = "",
            bool mirrored = false)
            : this(
                characterId,
                appearanceId,
                string.Empty,
                poseId,
                expressionId,
                animationId,
                mirrored)
        {
        }

        public CharacterPresentation(
            string characterId,
            string appearanceId,
            string visualStateId,
            string poseId,
            string expressionId,
            string animationId,
            bool mirrored)
        {
            CharacterId = characterId ?? string.Empty;
            AppearanceId = appearanceId ?? string.Empty;
            VisualStateId = visualStateId ?? string.Empty;
            PoseId = poseId ?? string.Empty;
            ExpressionId = expressionId ?? string.Empty;
            AnimationId = animationId ?? string.Empty;
            Mirrored = mirrored;
        }

        public string CharacterId { get; }
        public string AppearanceId { get; }
        public string VisualStateId { get; }
        public string PoseId { get; }
        public string ExpressionId { get; }
        public string AnimationId { get; }
        public bool Mirrored { get; }
    }
}
