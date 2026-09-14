using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationNodeChoiceCardList : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private ExplorationNodeChoiceCardView cardPrefab;
        [SerializeField] private ExplorationNodeChoiceLayoutGroup layout;
        private readonly List<ExplorationNodeChoiceCardView> cards = new();

        public IReadOnlyList<ExplorationNodeChoiceCardView> Cards => cards;
        public ExplorationNodeChoiceLayoutGroup Layout => layout;

        public void Show(IReadOnlyList<ExplorationNodeChoiceCardData> items, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            EnsureCapacity(items.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                if (i < items.Count) cards[i].Bind(items[i], onSelected);
                else { cards[i].Unbind(); cards[i].gameObject.SetActive(false); }
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.verticalNormalizedPosition = 1f;
            if (items.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cards[0].Button.gameObject);
        }

        public void Hide()
        {
            foreach (var card in cards) { card.Unbind(); card.gameObject.SetActive(false); }
        }

        private void EnsureCapacity(int count)
        {
            if (cardPrefab == null || content == null) throw new InvalidOperationException("Exploration card list references are incomplete.");
            while (cards.Count < count)
            {
                var instance = Instantiate(cardPrefab, content);
                instance.name = $"ChoiceCard{cards.Count + 1}";
                cards.Add(instance);
            }
        }

        private void OnDestroy() { foreach (var card in cards) if (card != null) card.Unbind(); }

#if UNITY_EDITOR
        public void ConfigureForEditor(ScrollRect targetScrollRect, RectTransform targetContent,
            ExplorationNodeChoiceCardView targetPrefab, ExplorationNodeChoiceLayoutGroup targetLayout)
        { scrollRect = targetScrollRect; content = targetContent; cardPrefab = targetPrefab; layout = targetLayout; }
#endif
    }
}
