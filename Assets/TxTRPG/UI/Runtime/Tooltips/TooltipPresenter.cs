using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TxTRPG.UI.Tooltips
{
    // The only owner of the open tooltip chain (one per TooltipLayer). Depth 0 is the root tooltip (a rune card), depths
    // 1..maximumKeywordDepth are keyword tooltips, each opened from a keyword of the tooltip above it.
    // Rules: a new root closes the whole chain; a keyword tooltip opens after the pointer stayed showDelay on the keyword
    // and replaces any other tooltip at that depth (and below); a keyword already open in the chain or past the maximum
    // depth opens nothing. A tooltip stays while the pointer is over its anchor, its box or any deeper tooltip, and closes
    // hideGraceSeconds after that stops (with everything below it). An invalid or moved anchor, or a frame scale or size
    // change, closes at once. Placement is computed only when a tooltip opens (TooltipPlacement in this layer's space,
    // inside the screen frame). ShowRoot and HideAll take no pointer, so focus or tap paths can call them later.
    [DisallowMultipleComponent]
    public sealed class TooltipPresenter : MonoBehaviour
    {
        public const int DefaultMaximumKeywordDepth = 3;
        public const float DefaultRootShowDelay = 0f;
        public const float DefaultKeywordShowDelay = .1f;
        public const float DefaultHideGrace = .08f;
        public const float DefaultFrameMargin = 8f;
        private const float MoveTolerance = .5f;
        private static readonly Vector3[] Corners = new Vector3[4];

        [SerializeField] private TooltipView viewPrefab;
        [SerializeField] private TooltipKeywordCatalog keywordCatalog;
        [SerializeField, Tooltip("ScreenFrame RectTransform: tooltip boxes stay inside it.")] private RectTransform frame;
        [SerializeField, Min(0)] private int maximumKeywordDepth = DefaultMaximumKeywordDepth;
        [SerializeField, Min(0f)] private float rootShowDelaySeconds = DefaultRootShowDelay;
        [SerializeField, Min(0f)] private float keywordShowDelaySeconds = DefaultKeywordShowDelay;
        [SerializeField, Min(0f)] private float hideGraceSeconds = DefaultHideGrace;
        [SerializeField, Min(0f)] private float frameMargin = DefaultFrameMargin;
        [SerializeField, Min(0f)] private float minimumWidth = TooltipView.DefaultMinimumWidth;
        [SerializeField, Min(0f)] private float maximumWidth = TooltipView.DefaultMaximumWidth;

        private sealed class Entry
        {
            public ITooltipAnchor Anchor;
            public TooltipView View;
            public string KeywordId;
            public Rect AnchorRect;
            public float HideStart = -1f;
            public TooltipPlacementResult Placement;
        }

        private sealed class KeywordAnchor : ITooltipAnchor
        {
            public TooltipKeywordLinkDetector Detector;
            public string LinkId;
            public int Version;

            public bool IsAnchorValid => Detector != null && Detector.isActiveAndEnabled && Detector.Version == Version;
            public bool IsPointerOver => IsAnchorValid && string.Equals(Detector.CurrentLinkId, LinkId, StringComparison.Ordinal);
            public bool TryGetAnchorRect(RectTransform space, out Rect rect)
            {
                rect = default;
                return IsAnchorValid && Detector.TryGetLinkRect(LinkId, space, out rect);
            }
        }

        private readonly List<Entry> chain = new();
        private readonly List<TooltipView> views = new();
        private readonly HashSet<string> warned = new(StringComparer.Ordinal);
        private ITooltipAnchor pendingRootAnchor;
        private IReadOnlyList<TooltipBlock> pendingRootBlocks;
        private float pendingRootStart;
        private TooltipKeywordLinkDetector pendingDetector;
        private string pendingLinkId = string.Empty;
        private int pendingDepth;
        private float pendingStart;
        private Vector2 frameSize;
        private Vector3 frameScale;
        private float now;
        private bool reportedMissing;

        public TooltipKeywordCatalog KeywordCatalog => keywordCatalog;
        public RectTransform Frame => frame;
        public int MaximumKeywordDepth => Mathf.Max(0, maximumKeywordDepth);
        public float KeywordShowDelaySeconds => keywordShowDelaySeconds;
        public float HideGraceSeconds => hideGraceSeconds;
        public float FrameMargin => frameMargin;
        public int OpenCount => chain.Count;
        public RectTransform Space => (RectTransform)transform;
        public bool HasPendingKeyword => pendingDetector != null;

        public TooltipView GetView(int depth) => depth >= 0 && depth < chain.Count ? chain[depth].View : null;
        public ITooltipAnchor GetAnchor(int depth) => depth >= 0 && depth < chain.Count ? chain[depth].Anchor : null;
        public string GetKeywordId(int depth) => depth >= 0 && depth < chain.Count ? chain[depth].KeywordId : string.Empty;
        public TooltipPlacementResult GetPlacement(int depth) => depth >= 0 && depth < chain.Count ? chain[depth].Placement : default;

        public bool IsValid(out string missing)
        {
            missing = viewPrefab == null ? nameof(viewPrefab) : frame == null ? nameof(frame) : string.Empty;
            return missing.Length == 0;
        }

        // Mouse-only scope: touch pointers never open or keep tooltips. A pen counts as a mouse.
        public static bool IsTouch(PointerEventData eventData) =>
            eventData is ExtendedPointerEventData extended && extended.pointerType == UIPointerType.Touch;

        // Pointer path of a root anchor: opens after rootShowDelaySeconds while the pointer stays on it. The same anchor
        // already open as the root is kept.
        public void RequestRoot(ITooltipAnchor anchor, IReadOnlyList<TooltipBlock> blocks)
        {
            if (anchor == null) return;
            if (chain.Count > 0 && ReferenceEquals(chain[0].Anchor, anchor)) return;
            if (rootShowDelaySeconds <= 0f) { ShowRoot(anchor, blocks); return; }
            pendingRootAnchor = anchor;
            pendingRootBlocks = blocks;
            pendingRootStart = now;
        }

        public void ShowRoot(ITooltipAnchor anchor, TooltipContent content) =>
            ShowRoot(anchor, content != null ? content.Blocks : null);

        // Opens a root tooltip at once, closing the current chain. Returns false when it cannot be shown.
        public bool ShowRoot(ITooltipAnchor anchor, IReadOnlyList<TooltipBlock> blocks)
        {
            ClearPendingRoot();
            HideAll();
            if (anchor == null || !anchor.IsAnchorValid || !EnsureValid()) return false;
            return Open(0, anchor, blocks, string.Empty);
        }

        // Closes the tooltip opened for this anchor (and everything below it).
        public void Hide(ITooltipAnchor anchor)
        {
            if (ReferenceEquals(pendingRootAnchor, anchor)) ClearPendingRoot();
            for (var i = 0; i < chain.Count; i++)
                if (ReferenceEquals(chain[i].Anchor, anchor)) { CloseFrom(i); return; }
        }

        public void HideAll()
        {
            ClearPendingRoot();
            ClearPendingKeyword();
            CloseFrom(0);
        }

        private void Update() => Tick(Time.unscaledTime);

        // One evaluation of the chain at time `time` (seconds). Update calls it; Edit Mode tests call it directly.
        public void Tick(float time)
        {
            now = time;
            if (pendingRootAnchor != null)
            {
                if (!pendingRootAnchor.IsAnchorValid || !pendingRootAnchor.IsPointerOver) ClearPendingRoot();
                else if (now - pendingRootStart >= rootShowDelaySeconds) ShowRoot(pendingRootAnchor, pendingRootBlocks);
            }
            if (chain.Count == 0) return;
            if (FrameChanged()) { HideAll(); return; }

            for (var i = 0; i < chain.Count; i++)
            {
                var entry = chain[i];
                if (!AnchorStillThere(entry)) { CloseFrom(i); break; }
            }

            // Deepest first: a tooltip is kept while the pointer is over its anchor, its box or anything deeper.
            var deeper = false;
            var closeFrom = -1;
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var entry = chain[i];
                var over = deeper || entry.Anchor.IsPointerOver || (entry.View != null && entry.View.IsPointerOver);
                if (over) entry.HideStart = -1f;
                else if (entry.HideStart < 0f) entry.HideStart = now;
                else if (now - entry.HideStart >= hideGraceSeconds) closeFrom = i;
                deeper = over;
            }
            if (closeFrom >= 0) CloseFrom(closeFrom);

            if (pendingDetector != null)
            {
                if (pendingDepth > chain.Count || !pendingDetector.isActiveAndEnabled ||
                    !string.Equals(pendingDetector.CurrentLinkId, pendingLinkId, StringComparison.Ordinal)) ClearPendingKeyword();
                else if (now - pendingStart >= keywordShowDelaySeconds) OpenPendingKeyword();
            }
        }

        private void OnKeywordLinkChanged(TooltipView view, TooltipKeywordLinkDetector detector, string linkId)
        {
            var depth = IndexOfView(view) + 1;
            if (depth <= 0) return;
            if (string.IsNullOrEmpty(linkId))
            {
                if (pendingDetector == detector) ClearPendingKeyword();
                return;
            }
            var keywordId = TooltipMarkup.KeywordIdOfLink(linkId);
            if (keywordId.Length == 0 || depth > MaximumKeywordDepth || keywordCatalog == null || !keywordCatalog.TryGet(keywordId, out _)) return;
            if (depth < chain.Count && chain[depth].Anchor is KeywordAnchor open && open.Detector == detector &&
                string.Equals(open.LinkId, linkId, StringComparison.Ordinal)) return;
            // A keyword already open above this depth would only repeat the cycle.
            for (var i = 0; i < depth && i < chain.Count; i++)
                if (string.Equals(chain[i].KeywordId, keywordId, StringComparison.Ordinal)) return;
            pendingDetector = detector;
            pendingLinkId = linkId;
            pendingDepth = depth;
            pendingStart = now;
            if (keywordShowDelaySeconds <= 0f) OpenPendingKeyword();
        }

        private void OpenPendingKeyword()
        {
            var detector = pendingDetector;
            var linkId = pendingLinkId;
            var depth = pendingDepth;
            ClearPendingKeyword();
            var keywordId = TooltipMarkup.KeywordIdOfLink(linkId);
            if (keywordCatalog == null || !keywordCatalog.TryGet(keywordId, out var keyword)) return;
            CloseFrom(depth);
            var anchor = new KeywordAnchor { Detector = detector, LinkId = linkId, Version = detector.Version };
            Open(depth, anchor, keyword.CreateTooltipBlocks(), keyword.Id);
        }

        private bool Open(int depth, ITooltipAnchor anchor, IReadOnlyList<TooltipBlock> blocks, string keywordId)
        {
            var space = Space;
            if (!anchor.TryGetAnchorRect(space, out var anchorRect)) return false;
            var frameRect = FrameRect();
            var view = ViewAt(depth);
            view.Rect.SetAsLastSibling();
            var size = view.Show(blocks, keywordCatalog, minimumWidth, maximumWidth, Mathf.Max(0f, frameRect.height - 2f * frameMargin), out var report);
            Report(blocks, keywordId, report);
            var placement = TooltipPlacement.Place(anchorRect, size, frameRect, frameMargin);
            view.Rect.localPosition = new Vector3(placement.TopLeft.x, placement.TopLeft.y, 0f);
            chain.Add(new Entry { Anchor = anchor, View = view, KeywordId = keywordId ?? string.Empty, AnchorRect = anchorRect, Placement = placement });
            if (depth == 0)
            {
                frameSize = frame.rect.size;
                frameScale = frame.lossyScale;
            }
            return true;
        }

        private void CloseFrom(int depth)
        {
            for (var i = chain.Count - 1; i >= depth && i >= 0; i--)
            {
                if (chain[i].View != null) chain[i].View.Hide();
                chain.RemoveAt(i);
            }
            if (pendingDetector != null && pendingDepth > chain.Count) ClearPendingKeyword();
        }

        private bool AnchorStillThere(Entry entry)
        {
            if (entry.Anchor == null || !entry.Anchor.IsAnchorValid) return false;
            if (!entry.Anchor.TryGetAnchorRect(Space, out var rect)) return false;
            return Mathf.Abs(rect.x - entry.AnchorRect.x) <= MoveTolerance && Mathf.Abs(rect.y - entry.AnchorRect.y) <= MoveTolerance &&
                Mathf.Abs(rect.width - entry.AnchorRect.width) <= MoveTolerance && Mathf.Abs(rect.height - entry.AnchorRect.height) <= MoveTolerance;
        }

        private bool FrameChanged() =>
            frame == null || (frame.rect.size - frameSize).sqrMagnitude > MoveTolerance * MoveTolerance || (frame.lossyScale - frameScale).sqrMagnitude > 1e-8f;

        private Rect FrameRect()
        {
            frame.GetWorldCorners(Corners);
            Vector2 min = Space.InverseTransformPoint(Corners[0]);
            Vector2 max = Space.InverseTransformPoint(Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private TooltipView ViewAt(int depth)
        {
            while (views.Count <= depth)
            {
                var view = Instantiate(viewPrefab, transform, false);
                view.name = $"{viewPrefab.name} {views.Count}";
                view.KeywordLinkChanged += OnKeywordLinkChanged;
                view.Hide();
                views.Add(view);
            }
            return views[depth];
        }

        private int IndexOfView(TooltipView view)
        {
            for (var i = 0; i < chain.Count; i++) if (chain[i].View == view) return i;
            return -1;
        }

        private void ClearPendingRoot()
        {
            pendingRootAnchor = null;
            pendingRootBlocks = null;
        }

        private void ClearPendingKeyword()
        {
            pendingDetector = null;
            pendingLinkId = string.Empty;
            pendingDepth = 0;
        }

        // Editor and Development Build only, once per problem.
        private void Report(IReadOnlyList<TooltipBlock> blocks, string keywordId, TooltipShowReport report)
        {
            if (!Debug.isDebugBuild) return;
            var subject = keywordId.Length > 0 ? $"keyword '{keywordId}'" : $"tooltip with {(blocks?.Count ?? 0)} blocks";
            if (report.UnknownBlocks > 0) WarnOnce($"blocks:{subject}", $"Tooltip {subject} has {report.UnknownBlocks} block(s) of unknown kind; they are not shown.");
            foreach (var id in report.UnknownKeywordIds) WarnOnce($"keyword:{id}", $"Tooltip keyword '{id}' is not in the keyword catalog; it is shown as plain text.");
            if (report.HasUnclosedMarkup) WarnOnce($"unclosed:{subject}", $"Tooltip {subject} has an unclosed {{kw: markup; it is shown as written.");
            if (report.Clipped) WarnOnce($"clipped:{subject}", $"Tooltip {subject} is taller than the screen frame and is clipped.");
        }

        private void WarnOnce(string key, string message)
        {
            if (warned.Add(key)) Debug.LogWarning(message, this);
        }

        private bool EnsureValid()
        {
            if (IsValid(out var missing)) return true;
            if (!reportedMissing)
            {
                reportedMissing = true;
                Debug.LogError($"TooltipPresenter is missing its '{missing}' reference; tooltips are not shown.", this);
            }
            return false;
        }

        private void OnDisable() => HideAll();

#if UNITY_EDITOR
        public void ConfigureForEditor(TooltipView prefab, TooltipKeywordCatalog catalog, RectTransform screenFrame)
        {
            viewPrefab = prefab;
            keywordCatalog = catalog;
            frame = screenFrame;
        }

        public void SetTimingForEditor(float rootDelay, float keywordDelay, float grace, int maximumDepth)
        {
            rootShowDelaySeconds = Mathf.Max(0f, rootDelay);
            keywordShowDelaySeconds = Mathf.Max(0f, keywordDelay);
            hideGraceSeconds = Mathf.Max(0f, grace);
            maximumKeywordDepth = Mathf.Max(0, maximumDepth);
        }
#endif
    }
}
