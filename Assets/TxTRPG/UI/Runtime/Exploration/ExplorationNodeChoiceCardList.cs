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
        private int generation;
        private bool selectionPending;

        public IReadOnlyList<ExplorationNodeChoiceCardView> Cards => cards;
        public ExplorationNodeChoiceLayoutGroup Layout => layout;

        public void Show(IReadOnlyList<ExplorationNodeChoiceCardData> items, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            generation++; selectionPending = false; StopAllCoroutines();
            EnsureCapacity(items.Count);
            for (var i = 0; i < cards.Count; i++)
            {
                if (i < items.Count)
                {
                    var card = cards[i]; var currentGeneration = generation;
                    card.Bind(items[i], request => RequestSelection(card, request, currentGeneration, onSelected));
                }
                else { cards[i].Unbind(); cards[i].gameObject.SetActive(false); }
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            scrollRect.verticalNormalizedPosition = 1f;
            if (items.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cards[0].Button.gameObject);
        }

        public void Hide()
        {
            generation++; selectionPending = false; StopAllCoroutines();
            foreach (var card in cards) { card.Unbind(); card.gameObject.SetActive(false); }
        }

        public void SetInteractable(bool value)
        {
            if (value) selectionPending = false;
            foreach (var card in cards) if (card != null && card.gameObject.activeSelf) card.Button.interactable = value;
        }

        private void RequestSelection(ExplorationNodeChoiceCardView card, ExplorationNodeChoiceRequest request, int requestGeneration, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            if (selectionPending || requestGeneration != generation || card == null || !card.gameObject.activeInHierarchy) return;
            selectionPending = true;
            foreach (var item in cards) if (item != null) item.Button.interactable = false;
            StartCoroutine(ConfirmAndSelect(card, request, requestGeneration, onSelected));
        }

        private System.Collections.IEnumerator ConfirmAndSelect(ExplorationNodeChoiceCardView card, ExplorationNodeChoiceRequest request, int requestGeneration, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            var animation = card.PlayConfirmation();
            while (true)
            {
                bool moveNext;
                try { moveNext = animation.MoveNext(); }
                catch (Exception exception) { Debug.LogWarning($"Exploration card confirmation was skipped: {exception.Message}", card); break; }
                if (!moveNext) break;
                yield return animation.Current;
            }
            if (requestGeneration != generation || card == null || !card.gameObject.activeInHierarchy) yield break;
            onSelected?.Invoke(request);
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

        private void OnDisable() => Hide();
        private void OnDestroy() { foreach (var card in cards) if (card != null) card.Unbind(); }

#if UNITY_EDITOR
        public void ConfigureForEditor(ScrollRect targetScrollRect, RectTransform targetContent,
            ExplorationNodeChoiceCardView targetPrefab, ExplorationNodeChoiceLayoutGroup targetLayout)
        { scrollRect = targetScrollRect; content = targetContent; cardPrefab = targetPrefab; layout = targetLayout; }
#endif
    }
}
