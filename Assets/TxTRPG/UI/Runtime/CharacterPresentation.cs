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
        {
            CharacterId = characterId ?? string.Empty;
            AppearanceId = appearanceId ?? string.Empty;
            PoseId = poseId ?? string.Empty;
            ExpressionId = expressionId ?? string.Empty;
            AnimationId = animationId ?? string.Empty;
            Mirrored = mirrored;
        }

        public string CharacterId { get; }
        public string AppearanceId { get; }
        public string PoseId { get; }
        public string ExpressionId { get; }
        public string AnimationId { get; }
        public bool Mirrored { get; }
    }
}
