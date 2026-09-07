using System.Collections;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class HealthBarScalePulseEffect : HealthBarEffect
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField, Min(1f)] private float peakScale = 1.08f;
        [SerializeField, Min(0f)] private float duration = 0.18f;
        [SerializeField, Min(0.001f)] private float maximumFrameDelta = 0.05f;
        [SerializeField] private bool useUnscaledTime = true;

        private Coroutine routine;
        private Vector3 restScale = Vector3.one;
        private bool hasRestScale;

        public void Configure(RectTransform target, float scale = 1.08f, float seconds = 0.18f)
        {
            Clear();
            visualRoot = target;
            peakScale = Mathf.Max(1f, scale);
            duration = Mathf.Max(0f, seconds);
            CaptureRestScale();
        }

        public override void Apply(
            in HealthPresentation previous,
            in HealthPresentation current,
            bool isInitialValue)
        {
            if (isInitialValue || current.Current >= previous.Current || !isActiveAndEnabled)
            {
                return;
            }

            var target = ResolveTarget();
            if (target == null) return;
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
                if (hasRestScale) target.localScale = restScale;
            }
            CaptureRestScale();
            if (!Application.isPlaying || duration <= 0f)
            {
                target.localScale = restScale;
                return;
            }
            routine = StartCoroutine(PlayPulse(target));
        }

        public override void Clear()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            var target = ResolveTarget();
            if (target != null && hasRestScale) target.localScale = restScale;
        }

        private IEnumerator PlayPulse(RectTransform target)
        {
            var elapsed = 0f;
            target.localScale = restScale * peakScale;
            while (elapsed < duration)
            {
                yield return null;
                var frameDelta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                elapsed += Mathf.Min(Mathf.Max(0f, frameDelta), maximumFrameDelta);
                var progress = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.LerpUnclamped(restScale * peakScale, restScale, progress);
            }
            target.localScale = restScale;
            routine = null;
        }

        private RectTransform ResolveTarget()
        {
            if (visualRoot != null) return visualRoot;
            return GetComponentInParent<HealthBarPanel>()?.BarVisualRoot;
        }

        private void CaptureRestScale()
        {
            var target = ResolveTarget();
            if (target == null) return;
            restScale = target.localScale;
            hasRestScale = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            peakScale = Mathf.Max(1f, peakScale);
            duration = Mathf.Max(0f, duration);
            maximumFrameDelta = Mathf.Max(0.001f, maximumFrameDelta);
        }
#endif
    }
}
