using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Runes
{
    // Display-only grid of rune cards inside a vertical-only ScrollRect. Receives display data (no Gameplay or Application
    // types). Cards come from a pool under Content: extra cards are deactivated, never destroyed, and repeating the same
    // data instantiates nothing. A missing icon catalog, a catalog that fails Validate or a missing Sprite shows the
    // name-only fallback card. Missing required references log one error and stop displaying; nothing throws.
    [DisallowMultipleComponent]
    public sealed class RuneGridPanel : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RuneGridLayoutGroup layoutGroup;
        [SerializeField] private RuneGridCardView cardPrefab;
        [SerializeField] private TMP_Text messageText;
        [SerializeField, Tooltip("Optional. Without it, or when it fails validation, every card shows the rune name.")]
        private RuneIconCatalog iconCatalog;

        private readonly List<RuneGridCardView> cards = new();
        private readonly List<RuneGridEntryDisplay> entries = new();
        private bool reportedMissingReference;
        private bool catalogChecked;
        private bool catalogUsable;
        private bool showingMessage;

        public ScrollRect ScrollRect => scrollRect;
        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public RuneGridLayoutGroup LayoutGroup => layoutGroup;
        public TMP_Text MessageText => messageText;
        public RuneIconCatalog IconCatalog => iconCatalog;
        public IReadOnlyList<RuneGridCardView> Cards => cards;
        public int ActiveCardCount { get; private set; }
        public bool IsShowingMessage => showingMessage;

        public bool IsValid(out string missing)
        {
            missing = scrollRect == null ? nameof(scrollRect) : viewport == null ? nameof(viewport) : content == null ? nameof(content)
                : layoutGroup == null ? nameof(layoutGroup) : cardPrefab == null ? nameof(cardPrefab) : messageText == null ? nameof(messageText) : string.Empty;
            return missing.Length == 0;
        }

        private void Awake()
        {
            // The grid adapts its columns to the width, so horizontal scrolling is never needed.
            if (scrollRect != null) scrollRect.horizontal = false;
        }

        public void SetIconCatalog(RuneIconCatalog catalog)
        {
            iconCatalog = catalog;
            catalogChecked = false;
            if (!showingMessage && ActiveCardCount > 0) Rebind();
        }

        public void SetEntries(IReadOnlyList<RuneGridEntryDisplay> source)
        {
            if (!EnsureValid()) return;
            entries.Clear();
            if (source != null)
                for (var i = 0; i < source.Count; i++) entries.Add(source[i]);
            showingMessage = false;
            messageText.gameObject.SetActive(false);
            Rebind();
        }

        public void ShowMessage(string message)
        {
            if (!EnsureValid()) return;
            entries.Clear();
            showingMessage = true;
            for (var i = 0; i < ActiveCardCount; i++) Release(cards[i]);
            ActiveCardCount = 0;
            messageText.text = message ?? string.Empty;
            messageText.gameObject.SetActive(true);
            MarkLayout();
        }

        private void Rebind()
        {
            var useCatalog = CatalogUsable();
            var settings = layoutGroup.Settings;
            for (var i = 0; i < entries.Count; i++)
            {
                var card = i < cards.Count ? cards[i] : CreateCard();
                if (!card.gameObject.activeSelf) card.gameObject.SetActive(true);
                card.ApplyProportions(settings.BadgeSizeRatio, settings.BadgeOverhangRatio);
                Sprite icon = null;
                if (useCatalog) iconCatalog.TryGetIcon(entries[i].DefinitionId, out icon);
                card.Bind(entries[i], icon);
            }
            for (var i = entries.Count; i < ActiveCardCount; i++) Release(cards[i]);
            ActiveCardCount = entries.Count;
            MarkLayout();
        }

        private RuneGridCardView CreateCard()
        {
            var card = Instantiate(cardPrefab, content, false);
            card.name = $"{cardPrefab.name} {cards.Count + 1}";
            cards.Add(card);
            return card;
        }

        private static void Release(RuneGridCardView card)
        {
            card.Clear();
            card.gameObject.SetActive(false);
        }

        private bool CatalogUsable()
        {
            if (catalogChecked) return catalogUsable;
            catalogChecked = true;
            catalogUsable = false;
            if (iconCatalog == null)
                Debug.LogWarning("RuneGridPanel has no RuneIconCatalog; rune cards show their names.", this);
            else if (!iconCatalog.Validate(out var reason))
                Debug.LogWarning($"RuneIconCatalog '{iconCatalog.name}' is invalid ({reason}); rune cards show their names.", this);
            else catalogUsable = true;
            return catalogUsable;
        }

        private bool EnsureValid()
        {
            if (IsValid(out var missing)) return true;
            if (!reportedMissingReference)
            {
                reportedMissingReference = true;
                Debug.LogError($"RuneGridPanel is missing its '{missing}' reference; the rune grid is not displayed.", this);
            }
            return false;
        }

#if UNITY_EDITOR
        // An Inspector change of the icon catalog during Play re-binds the shown cards (deferred: OnValidate must not
        // toggle GameObjects).
        private void OnValidate()
        {
            catalogChecked = false;
            if (!UnityEngine.Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && !showingMessage && ActiveCardCount > 0) Rebind();
            };
        }
#endif

        // One layout pass after all cards changed.
        private void MarkLayout() => LayoutRebuilder.MarkLayoutForRebuild(content);
    }
}
