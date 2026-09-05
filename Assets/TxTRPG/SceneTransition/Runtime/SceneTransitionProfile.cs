using UnityEngine;

namespace TxTRPG.SceneTransition
{
    public enum ReducedMotionTransitionMode
    {
        ShortFade,
        Instant
    }

    [CreateAssetMenu(
        fileName = "SceneTransitionProfile",
        menuName = "TxT RPG/Scene Transition/Profile")]
    public sealed class SceneTransitionProfile : ScriptableObject
    {
        [SerializeField] private Color color = Color.black;
        [SerializeField, Min(0f)] private float coverDuration = 0.25f;
        [SerializeField, Min(0f)] private float revealDuration = 0.25f;
        [SerializeField, Min(0f)] private float minimumCoveredTime = 0.1f;
        [SerializeField, Min(0f)] private float readinessTimeout = 30f;
        [SerializeField, Range(0.001f, 0.1f)] private float maximumFrameDelta = 0.05f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool blockInput = true;
        [SerializeField] private ReducedMotionTransitionMode reducedMotionMode =
            ReducedMotionTransitionMode.ShortFade;
        [SerializeField, Min(0f)] private float reducedMotionDuration = 0.08f;

        public Color Color => color;
        public float CoverDuration => Mathf.Max(0f, coverDuration);
        public float RevealDuration => Mathf.Max(0f, revealDuration);
        public float MinimumCoveredTime => Mathf.Max(0f, minimumCoveredTime);
        public float ReadinessTimeout => Mathf.Max(0f, readinessTimeout);
        public float MaximumFrameDelta => Mathf.Clamp(maximumFrameDelta, 0.001f, 0.1f);
        public bool UseUnscaledTime => useUnscaledTime;
        public bool BlockInput => blockInput;
        public ReducedMotionTransitionMode ReducedMotionMode => reducedMotionMode;
        public float ReducedMotionDuration => Mathf.Max(0f, reducedMotionDuration);
    }
}
