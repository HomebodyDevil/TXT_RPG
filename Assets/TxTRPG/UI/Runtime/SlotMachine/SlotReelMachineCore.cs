using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // References and callbacks a reel machine owner (SlotMachinePanel, SlotReelDock) hands to SlotReelMachineCore. The
    // core reads them on every use, so an owner may reassign its serialized fields (for example ConfigureForEditor).
    internal interface ISlotReelMachineHost
    {
        // Owning component: lifetime checks, logging context and isActiveAndEnabled.
        MonoBehaviour Owner { get; }
        RectTransform ReelArea { get; }
        SlotReelViewBase ReelPrefab { get; }
        List<SlotReelViewBase> ReelViews { get; }
        // The first divider is the template for added dividers.
        List<RectTransform> ReelDividers { get; }
        // Layout-only payline root; null keeps the old single-graphic frame.
        RectTransform PaylineRoot { get; }
        SlotPaylineFrameGraphic PaylineFrame { get; }
        Image PaylineImage { get; }
        float MinimumFontSize { get; }
        float MaximumFontSize { get; }
        SlotReelSpinSettings SpinSettings { get; }
        // True once the owner resolved its skin; reels and dividers added before that keep the Prefab default.
        bool SkinResolved { get; }
        SlotMachinePanelSkin ActiveSkin { get; }
        void ApplyDividerSkin(RectTransform divider);
        // Summary shown while the reels spin and after they stop.
        void SetSummary(SlotSummaryDisplay summary);
    }

    // Reel, divider, payline and spin handling shared by SlotMachinePanel and SlotReelDock. A plain class rather than a
    // component so the owners keep their serialized fields (and their Prefabs keep their serialization). It owns the
    // spin state: the generation number that invalidates a running presentation, the reel configuration deferred while
    // spinning, and the work buffers, which are created once so a spin does not allocate per frame.
    internal sealed class SlotReelMachineCore
    {
        private readonly ISlotReelMachineHost host;
        private readonly Vector3[] firstCorners = new Vector3[4];
        private readonly Vector3[] lastCorners = new Vector3[4];
        private readonly Action updatePaylineFrame;
        private int generation;
        private IReadOnlyList<SlotReelDisplay> pendingReels;
        private System.Random spinRandom;
        private SlotReelAreaLayout subscribedLayout;
        private float appliedBorderOutset = float.NaN;
        private Vector4 appliedImageOutset = new(float.NaN, 0f, 0f, 0f);

        public SlotReelMachineCore(ISlotReelMachineHost host)
        {
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            updatePaylineFrame = UpdatePaylineFrame;
        }

        public bool IsSpinning { get; private set; }

        public int ActiveDividerCount
        {
            get
            {
                var count = 0;
                foreach (var divider in host.ReelDividers) if (divider != null && divider.gameObject.activeSelf) count++;
                return count;
            }
        }

        public void SetRandomSeed(int seed) => spinRandom = new System.Random(seed);

        // Follows the reel area's layout passes with the payline frame (owner OnEnable).
        public void Enable(SlotReelAreaLayout layout)
        {
            Unsubscribe();
            if (layout != null)
            {
                layout.LayoutApplied += updatePaylineFrame;
                subscribedLayout = layout;
            }
            UpdatePaylineFrame();
        }

        // Owner OnDisable: stops following the layout and ends a running spin (its presentation returns false).
        public void Disable()
        {
            Unsubscribe();
            if (!IsSpinning) return;
            generation++;
            EndSpin();
        }

        private void Unsubscribe()
        {
            if (subscribedLayout != null) subscribedLayout.LayoutApplied -= updatePaylineFrame;
            subscribedLayout = null;
        }

        // Applies a reel configuration. While a spin is playing it is deferred until the spin ends, because the running
        // presentation always finishes with the accepted spin's strip copies. Reels whose ID and strip are unchanged keep
        // the cell they show; others show their first cell on the payline.
        public void SetReels(IReadOnlyList<SlotReelDisplay> reels)
        {
            if (reels == null || reels.Count == 0) throw new ArgumentException("At least one reel is required.", nameof(reels));
            if (IsSpinning) { pendingReels = reels; return; }
            pendingReels = null;
            if (!EnsureReelCount(reels.Count)) return;
            var views = host.ReelViews;
            for (var i = 0; i < reels.Count; i++)
            {
                var view = views[i];
                var keep = view.ReelId == reels[i].ReelId && SameValues(view.Strip, reels[i].Values);
                var stop = keep ? view.StopIndex : 0;
                view.ShowStop(reels[i], stop);
                // A kept reel still shows the result it stopped on, including its Fortune Critical multiplier.
                if (!keep) view.ShowFortuneMultiplier(string.Empty);
            }
        }

        // Shows the spinning summary, then (with progress summaries) the running total each time the reels from the first
        // one have stopped, and finally the result summary. With a total step the result waits
        // TotalCriticalRevealDelaySeconds after the last running total, except with Reduce Motion; IsSpinning stays true
        // during that wait. Returns false without the result summary when interrupted; the caller then calls StopAtResult.
        public async Task<bool> PlaySpinAsync(SlotSpinDisplay result, bool reduceMotion, CancellationToken cancellationToken)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var owner = host.Owner;
            if (owner == null || !owner.isActiveAndEnabled || !EnsureReelCount(result.Reels.Count)) return false;
            var token = ++generation;
            IsSpinning = true;
            host.SetSummary(result.SpinningSummary);
            try
            {
                // Every reel gets its plan first, then all start together and each waits for its own start delay.
                spinRandom ??= new System.Random();
                var settings = host.SpinSettings;
                var plans = SlotReelSpinPlanner.Create(settings, result.Reels.Count, reduceMotion, spinRandom);
                var progress = new SlotSpinProgress(result);
                var views = host.ReelViews;
                var tasks = new Task<bool>[result.Reels.Count];
                for (var i = 0; i < tasks.Length; i++)
                    tasks[i] = PlayReelAsync(views[i], result.Reels[i], i, progress, plans[i], token, cancellationToken);
                var completed = await Task.WhenAll(tasks);
                if (owner == null || token != generation) return false;
                foreach (var done in completed)
                    if (!done) return false;
                var delay = settings.TotalCriticalRevealDelaySeconds;
                if (result.HasProgressSummaries && result.HasTotalStep && !reduceMotion && delay > 0f)
                {
                    var start = Time.unscaledTime;
                    while (Time.unscaledTime - start < delay)
                    {
                        await Task.Yield();
                        if (Interrupted(owner, token, cancellationToken)) return false;
                    }
                }
                host.SetSummary(result.ResultSummary);
                return true;
            }
            finally
            {
                if (owner != null && token == generation) EndSpin();
            }
        }

        // The reel clears its multiplier when its spin starts and shows it once it is at rest on the result, followed by
        // the running total when it completes the run of stopped reels from the first one.
        private async Task<bool> PlayReelAsync(SlotReelViewBase view, SlotReelStopDisplay stop, int reelIndex, SlotSpinProgress progress,
            SlotReelSpinPlan plan, int token, CancellationToken cancellationToken)
        {
            view.ShowFortuneMultiplier(string.Empty);
            var done = await view.PlaySpinAsync(stop.Reel, stop.StopIndex, plan, cancellationToken);
            var owner = host.Owner;
            if (!done || owner == null || view == null || token != generation) return done;
            view.ShowFortuneMultiplier(stop.MultiplierText);
            var summary = progress.ReelStopped(reelIndex);
            if (summary != null && owner.isActiveAndEnabled) host.SetSummary(summary);
            return true;
        }

        private bool Interrupted(MonoBehaviour owner, int token, CancellationToken cancellationToken) =>
            owner == null || token != generation || cancellationToken.IsCancellationRequested || !owner.isActiveAndEnabled;

        // Puts every reel at rest on the accepted result immediately (timeout, cancellation or a restored result).
        public void StopAtResult(SlotSpinDisplay result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            generation++;
            if (EnsureReelCount(result.Reels.Count))
            {
                var views = host.ReelViews;
                for (var i = 0; i < result.Reels.Count; i++)
                {
                    views[i].ShowStop(result.Reels[i].Reel, result.Reels[i].StopIndex);
                    views[i].ShowFortuneMultiplier(result.Reels[i].MultiplierText);
                }
            }
            host.SetSummary(result.ResultSummary);
            EndSpin();
        }

        private void EndSpin()
        {
            IsSpinning = false;
            var pending = pendingReels;
            pendingReels = null;
            if (pending != null) SetReels(pending);
        }

        // Adds reels from the prefab or removes surplus ones, with one divider between neighbouring reels. Runs only
        // when the reel count changes.
        public bool EnsureReelCount(int count)
        {
            var views = host.ReelViews;
            var reelArea = host.ReelArea;
            var reelPrefab = host.ReelPrefab;
            views.RemoveAll(view => view == null);
            if (views.Count < count && (reelPrefab == null || reelArea == null)) return false;
            var changed = views.Count != count;
            while (views.Count < count)
            {
                var view = UnityEngine.Object.Instantiate(reelPrefab, reelArea, false);
                view.name = $"SlotReel_{views.Count + 1}";
                if (host.SkinResolved) view.ApplySkin(host.ActiveSkin);
                views.Add(view);
            }
            while (views.Count > count)
            {
                var last = views[views.Count - 1];
                views.RemoveAt(views.Count - 1);
                DestroyObject(last.gameObject);
            }
            changed |= EnsureDividers(count - 1);
            if (changed) ArrangeSiblings();
            // Fewer, wider reels get larger digits; the range keeps narrow screens and many reels readable.
            foreach (var view in views) view.SetFontSizeRange(host.MinimumFontSize, host.MaximumFontSize);
            if (changed) UpdatePaylineFrame();
            return true;
        }

        // The first divider is kept (inactive) as the template, so dividers can be added again after a single reel.
        private bool EnsureDividers(int count)
        {
            var dividers = host.ReelDividers;
            var reelArea = host.ReelArea;
            dividers.RemoveAll(divider => divider == null);
            if (dividers.Count == 0 || reelArea == null) return false;
            var changed = false;
            while (dividers.Count < count)
            {
                var divider = UnityEngine.Object.Instantiate(dividers[0], reelArea, false);
                divider.name = $"ReelDivider_{dividers.Count + 1}";
                if (host.SkinResolved) host.ApplyDividerSkin(divider);
                dividers.Add(divider);
                changed = true;
            }
            while (dividers.Count > Mathf.Max(1, count))
            {
                var last = dividers[dividers.Count - 1];
                dividers.RemoveAt(dividers.Count - 1);
                DestroyObject(last.gameObject);
                changed = true;
            }
            for (var i = 0; i < dividers.Count; i++)
            {
                var active = i < count;
                if (dividers[i].gameObject.activeSelf == active) continue;
                dividers[i].gameObject.SetActive(active);
                changed = true;
            }
            return changed;
        }

        // Reel, divider, reel, ... so a divider is only ever between two reels.
        private void ArrangeSiblings()
        {
            var views = host.ReelViews;
            var dividers = host.ReelDividers;
            var index = 0;
            for (var i = 0; i < views.Count; i++)
            {
                views[i].transform.SetSiblingIndex(index++);
                if (i < views.Count - 1 && i < dividers.Count) dividers[i].SetSiblingIndex(index++);
            }
        }

        // Exactly one payline display is enabled: the skin image when the skin has a payline sprite, otherwise the
        // code-drawn border (recolored when the skin overrides the color). Both keep their GameObjects. borderTarget is
        // the owner's recorded default for the border color.
        public void ApplyPaylineSkin(SlotSkinGraphicTarget borderTarget, UnityEngine.Object context)
        {
            var activeSkin = host.ActiveSkin;
            var paylineImage = host.PaylineImage;
            var paylineFrame = host.PaylineFrame;
            var entry = activeSkin != null ? activeSkin.PaylineFrame : null;
            var useImage = activeSkin != null && activeSkin.HasPaylineImage && paylineImage != null && host.PaylineRoot != null;
            if (paylineImage != null)
            {
                if (useImage)
                {
                    entry.WarnIfBorderMissing(context);
                    if (paylineImage.sprite != entry.Sprite) paylineImage.sprite = entry.Sprite;
                    if (paylineImage.type != entry.ImageType) paylineImage.type = entry.ImageType;
                    if (!Mathf.Approximately(paylineImage.pixelsPerUnitMultiplier, entry.PixelsPerUnitMultiplier))
                        paylineImage.pixelsPerUnitMultiplier = entry.PixelsPerUnitMultiplier;
                    var color = entry.OverrideColor ? entry.Color : Color.white;
                    if (paylineImage.color != color) paylineImage.color = color;
                }
                if (paylineImage.raycastTarget) paylineImage.raycastTarget = false;
                if (paylineImage.enabled != useImage) paylineImage.enabled = useImage;
            }
            if (paylineFrame != null)
            {
                // The border takes the color override only; a sprite applies to the image alone.
                borderTarget?.ApplyTo(paylineFrame, useImage || entry == null || !entry.OverrideColor ? null : entry, context);
                if (paylineFrame.enabled == useImage) paylineFrame.enabled = !useImage;
            }
        }

        // Places the payline root from the first reel window's left edge to the last one's right edge at the payline
        // cell height. Its children stretch over it with negative offsets: the border by its thickness, the image by the
        // skin's payline outset. Runs after reel area layout passes, reel count changes and skin changes only.
        // A Prefab not yet migrated (no root) keeps the old single-graphic frame, expanded by the border thickness.
        public void UpdatePaylineFrame()
        {
            var paylineRoot = host.PaylineRoot;
            var paylineFrame = host.PaylineFrame;
            var views = host.ReelViews;
            var frame = paylineRoot != null ? paylineRoot : paylineFrame != null ? paylineFrame.rectTransform : null;
            if (frame == null || views.Count == 0) return;
            if (!(frame.parent is RectTransform parent)) return;
            var first = views[0];
            var last = views[views.Count - 1];
            if (first == null || last == null || !first.GetPaylineWorldCorners(firstCorners) || !last.GetPaylineWorldCorners(lastCorners)) return;
            var min = (Vector2)parent.InverseTransformPoint(firstCorners[0]);
            var max = (Vector2)parent.InverseTransformPoint(lastCorners[2]);
            var expand = paylineRoot == null ? paylineFrame.EffectiveThickness : 0f;
            var size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) + Vector2.one * (2f * expand);
            var center = (min + max) * .5f - parent.rect.center;
            var middle = new Vector2(.5f, .5f);
            if (frame.anchorMin != middle) frame.anchorMin = middle;
            if (frame.anchorMax != middle) frame.anchorMax = middle;
            if (frame.pivot != middle) frame.pivot = middle;
            if (frame.sizeDelta != size) frame.sizeDelta = size;
            if (frame.anchoredPosition != center) frame.anchoredPosition = center;
            if (paylineRoot != null) UpdatePaylineChildren(paylineRoot, paylineFrame);
        }

        // Rewrites the child offsets only when the expansion changed (skin swap or canvas scale change).
        private void UpdatePaylineChildren(RectTransform paylineRoot, SlotPaylineFrameGraphic paylineFrame)
        {
            if (paylineFrame != null && paylineFrame.rectTransform.parent == paylineRoot)
            {
                var thickness = paylineFrame.EffectiveThickness;
                if (!Mathf.Approximately(thickness, appliedBorderOutset))
                {
                    appliedBorderOutset = thickness;
                    Expand(paylineFrame.rectTransform, thickness, thickness, thickness, thickness);
                }
            }
            var paylineImage = host.PaylineImage;
            if (paylineImage != null && paylineImage.rectTransform.parent == paylineRoot)
            {
                var activeSkin = host.ActiveSkin;
                var outset = activeSkin != null ? activeSkin.PaylineOutset : null;
                var value = outset != null ? new Vector4(outset.Left, outset.Right, outset.Top, outset.Bottom) : Vector4.zero;
                if (value != appliedImageOutset)
                {
                    appliedImageOutset = value;
                    Expand(paylineImage.rectTransform, value.x, value.y, value.z, value.w);
                }
            }
        }

        private static void Expand(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(-left, -bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void DestroyObject(GameObject target)
        {
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
        }

        private static bool SameValues(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
}
