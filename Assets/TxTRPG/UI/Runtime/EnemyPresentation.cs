namespace TxTRPG.UI
{
    public readonly struct EnemyPresentation
    {
        public EnemyPresentation(
            string instanceId,
            string enemyId,
            string appearanceId = "",
            string poseId = "",
            string animationId = "",
            int formationIndex = 0,
            bool mirrored = false,
            bool isTargeted = false,
            bool isDefeated = false)
        {
            InstanceId = instanceId ?? string.Empty;
            EnemyId = enemyId ?? string.Empty;
            AppearanceId = appearanceId ?? string.Empty;
            PoseId = poseId ?? string.Empty;
            AnimationId = animationId ?? string.Empty;
            FormationIndex = formationIndex < 0 ? 0 : formationIndex;
            Mirrored = mirrored;
            IsTargeted = isTargeted;
            IsDefeated = isDefeated;
        }

        public string InstanceId { get; }
        public string EnemyId { get; }
        public string AppearanceId { get; }
        public string PoseId { get; }
        public string AnimationId { get; }
        public int FormationIndex { get; }
        public bool Mirrored { get; }
        public bool IsTargeted { get; }
        public bool IsDefeated { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(InstanceId) &&
                               !string.IsNullOrWhiteSpace(EnemyId);

        public EnemyPresentation WithTargeted(bool targeted) => new(
            InstanceId, EnemyId, AppearanceId, PoseId, AnimationId,
            FormationIndex, Mirrored, targeted, IsDefeated);
    }
}
