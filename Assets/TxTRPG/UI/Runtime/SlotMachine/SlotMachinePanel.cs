using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Shows the slot machine reels, the payline total and the rune allocation text. It receives display data only and
    // never decides results: PlaySpinAsync presents an already accepted spin. The panel takes no input; every graphic
    // has raycastTarget off. Reels are SlotReelViewBase instances, so the reel implementation can be replaced.
    [DisallowMultipleComponent]
    public sealed class SlotMachinePanel : MonoBehaviour
    {
        [SerializeField] private RectTransform reelArea;
        [SerializeField, Tooltip("Reel added when the configuration has more reels than the panel shows.")]
        private SlotReelViewBase reelPrefab;
        [SerializeField] private List<SlotReelViewBase> reelViews = new();
        [SerializeField, Tooltip("Hidden while a configuration error is shown.")] private GameObject contentRoot;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text allocationText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField, Min(0f)] private float firstReelSpinSeconds = .8f;
        [SerializeField, Min(0f)] private float reelStopIntervalSeconds = .25f;
        [SerializeField, Min(0f)] private float decelerationSeconds = .3f;
        [SerializeField, Min(0f), Tooltip("With Reduce Motion, each reel shows its result after this delay, left to right.")]
        private float reducedMotionSecondsPerReel = .15f;
        [SerializeField, Min(1f)] private float minimumFontSize = 16f;
        [SerializeField, Min(1f)] private float maximumFontSize = 64f;

        private int generation;
        private IReadOnlyList<SlotReelDisplay> pendingReels;

        public bool IsSpinning { get; private set; }
        public int ReelCount => reelViews.Count;
        public IReadOnlyList<SlotReelViewBase> ReelViews => reelViews.AsReadOnly();
        public string TotalText => totalText != null ? totalText.text : string.Empty;
        public string AllocationText => allocationText != null ? allocationText.text : string.Empty;
        public string StatusText => statusText != null && statusText.gameObject.activeSelf ? statusText.text : string.Empty;
        public float MaximumPresentationSeconds => GetMaximumPresentationSeconds(Mathf.Max(1, reelViews.Count));

        public float GetMaximumPresentationSeconds(int reelCount)
        {
            reelCount = Mathf.Max(1, reelCount);
            var normal = firstReelSpinSeconds + (reelCount - 1) * reelStopIntervalSeconds + decelerationSeconds;
            var reduced = reelCount * reducedMotionSecondsPerReel;
            return Mathf.Max(normal, reduced);
        }

        private void Awake() => reelViews.RemoveAll(view => view == null);

        // Applies a reel configuration. While a spin is playing it is deferred until the spin ends, because the running
        // presentation always finishes with the accepted spin's strip copies. Reels whose ID and strip are unchanged keep
        // the cell they show; others show their first cell on the payline.
        public void SetReels(IReadOnlyList<SlotReelDisplay> reels)
        {
            if (reels == null || reels.Count == 0) throw new ArgumentException("At least one reel is required.", nameof(reels));
            if (IsSpinning) { pendingReels = reels; return; }
            pendingReels = null;
            if (!EnsureReelCount(reels.Count)) return;
            for (var i = 0; i < reels.Count; i++)
            {
                var view = reelViews[i];
                var keep = view.ReelId == reels[i].ReelId && SameValues(view.Strip, reels[i].Values);
                var stop = keep ? view.StopIndex : 0;
                view.ShowStop(reels[i], stop);
            }
        }

        public void SetTotalText(string value) { if (totalText != null) totalText.text = value ?? string.Empty; }
        public void SetAllocationText(string value) { if (allocationText != null) allocationText.text = value ?? string.Empty; }

        // Configuration error: the reels and summary are hidden and the reason is shown.
        public void ShowError(string message)
        {
            if (contentRoot != null) contentRoot.SetActive(false);
            SetStatus(message);
        }

        public void ClearError()
        {
            if (contentRoot != null) contentRoot.SetActive(true);
            SetStatus(string.Empty);
        }

        public async Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!isActiveAndEnabled || !EnsureReelCount(result.Reels.Count)) return false;
            var token = ++generation;
            IsSpinning = true;
            SetTotalText(result.SpinningTotalText);
            try
            {
                var tasks = new Task<bool>[result.Reels.Count];
                for (var i = 0; i < tasks.Length; i++)
                {
                    var timing = new SlotReelSpinTiming(firstReelSpinSeconds + i * reelStopIntervalSeconds,
                        decelerationSeconds, (i + 1) * reducedMotionSecondsPerReel, reduceMotion);
                    tasks[i] = reelViews[i].PlaySpinAsync(result.Reels[i].Reel, result.Reels[i].StopIndex, timing, cancellationToken);
                }
                var completed = await Task.WhenAll(tasks);
                if (this == null || token != generation) return false;
                foreach (var done in completed)
                    if (!done) return false;
                ShowResultTexts(result);
                return true;
            }
            finally
            {
                if (this != null && token == generation) EndSpin();
            }
        }

        // Puts every reel at rest on the accepted result immediately (timeout, cancellation or a restored result).
        public void StopAtResult(SlotSpinDisplay result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            generation++;
            if (EnsureReelCount(result.Reels.Count))
                for (var i = 0; i < result.Reels.Count; i++)
                    reelViews[i].ShowStop(result.Reels[i].Reel, result.Reels[i].StopIndex);
            ShowResultTexts(result);
            EndSpin();
        }

        private void OnDisable()
        {
            if (!IsSpinning) return;
            generation++;
            EndSpin();
        }

        private void EndSpin()
        {
            IsSpinning = false;
            var pending = pendingReels;
            pendingReels = null;
            if (pending != null) SetReels(pending);
        }

        private void ShowResultTexts(SlotSpinDisplay result)
        {
            SetTotalText(result.TotalText);
            SetAllocationText(result.AllocationText);
        }

        private void SetStatus(string message)
        {
            if (statusText == null) return;
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // Adds reels from the prefab or removes surplus ones. Runs only when the reel count changes.
        private bool EnsureReelCount(int count)
        {
            reelViews.RemoveAll(view => view == null);
            if (reelViews.Count < count && (reelPrefab == null || reelArea == null)) return false;
            while (reelViews.Count < count)
            {
                var view = Instantiate(reelPrefab, reelArea, false);
                view.name = $"SlotReel_{reelViews.Count + 1}";
                reelViews.Add(view);
            }
            while (reelViews.Count > count)
            {
                var last = reelViews[reelViews.Count - 1];
                reelViews.RemoveAt(reelViews.Count - 1);
                if (UnityEngine.Application.isPlaying) Destroy(last.gameObject); else DestroyImmediate(last.gameObject);
            }
            // Fewer, wider reels get larger digits; the range keeps narrow screens and many reels readable.
            foreach (var view in reelViews) view.SetFontSizeRange(minimumFontSize, maximumFontSize);
            return true;
        }

        private static bool SameValues(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++) if (left[i] != right[i]) return false;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetReelArea, SlotReelViewBase targetReelPrefab,
            IEnumerable<SlotReelViewBase> views, GameObject targetContent, TMP_Text targetTotal,
            TMP_Text targetAllocation, TMP_Text targetStatus)
        {
            reelArea = targetReelArea;
            reelPrefab = targetReelPrefab;
            reelViews = new List<SlotReelViewBase>(views ?? Array.Empty<SlotReelViewBase>());
            contentRoot = targetContent;
            totalText = targetTotal;
            allocationText = targetAllocation;
            statusText = targetStatus;
        }

        // Verification only: shortens the presentation so tests can finish without waiting for frames.
        public void SetTimingForEditor(float firstReel, float interval, float deceleration, float reducedPerReel)
        {
            firstReelSpinSeconds = Mathf.Max(0f, firstReel);
            reelStopIntervalSeconds = Mathf.Max(0f, interval);
            decelerationSeconds = Mathf.Max(0f, deceleration);
            reducedMotionSecondsPerReel = Mathf.Max(0f, reducedPerReel);
        }

        public RectTransform ReelAreaForEditor => reelArea;
        public SlotReelViewBase ReelPrefabForEditor => reelPrefab;
#endif
    }
}
