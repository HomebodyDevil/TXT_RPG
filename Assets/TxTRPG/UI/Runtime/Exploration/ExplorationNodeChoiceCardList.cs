using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using TxTRPG.UI.Windows;

namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class ExplorationNodeChoiceCardList : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private ExplorationNodeChoiceCardView cardPrefab;
        [SerializeField] private ExplorationNodeChoiceLayoutGroup layout;
        [SerializeField] private GameWindowService windowService;
        private bool showing, navigationArmed;
        private readonly Vector3[] focusCorners = new Vector3[4];
        private readonly List<ExplorationNodeChoiceCardView> cards = new();
        private int generation;
        private bool selectionPending;

        public IReadOnlyList<ExplorationNodeChoiceCardView> Cards => cards;
        public ExplorationNodeChoiceLayoutGroup Layout => layout;
        public GameWindowService WindowService => windowService;
        private bool InputBlocked => windowService != null && windowService.BlocksGameplayInput;

        public void Show(IReadOnlyList<ExplorationNodeChoiceCardData> items, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            ClearOwnedFocus();
            generation++; selectionPending = false; navigationArmed = false; StopAllCoroutines();
            showing = true;
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
        }

        public void Hide()
        {
            ClearOwnedFocus();
            showing = navigationArmed = false;
            generation++; selectionPending = false; StopAllCoroutines();
            foreach (var card in cards) if (card != null) { card.Unbind(); card.gameObject.SetActive(false); }
        }

        public void SetInteractable(bool value)
        {
            if (value) selectionPending = false;
            foreach (var card in cards) if (card != null && card.gameObject.activeSelf) card.Button.interactable = value;
        }

        private void RequestSelection(ExplorationNodeChoiceCardView card, ExplorationNodeChoiceRequest request, int requestGeneration, Action<ExplorationNodeChoiceRequest> onSelected)
        {
            if (!showing || InputBlocked || !isActiveAndEnabled || card == null || !card.Button.IsInteractable() || selectionPending || requestGeneration != generation || !card.gameObject.activeInHierarchy) return;
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
                instance.Focused += RevealFocus;
                cards.Add(instance);
            }
        }

        private void ClearOwnedFocus()
        {
            var events = EventSystem.current;
            if (events == null) return;
            foreach (var card in cards)
                if (card != null && events.currentSelectedGameObject == card.Button.gameObject)
                { events.SetSelectedGameObject(null); return; }
        }

        // Before EventSystem.Update: a loading modal can own input without a selected control.
        private void Update()
        {
            if (InputBlocked) { navigationArmed = false; ClearOwnedFocus(); }
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            var module = events != null ? events.currentInputModule as InputSystemUIInputModule : null;
            var move = module != null ? module.move?.action : null;
            if (!showing || selectionPending || InputBlocked || events == null || !events.sendNavigationEvents ||
                module == null || !module.isActiveAndEnabled || move == null || !move.enabled)
            { navigationArmed = false; return; }
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) { navigationArmed = false; return; }
            var neutral = move.ReadValue<Vector2>().sqrMagnitude < .0001f;
            if (neutral) { navigationArmed = true; return; }
            if (!navigationArmed) return;
            navigationArmed = false;
            // All EventSystem move/submit processing is finished for this frame. The entry
            // gesture cannot move twice or submit; held input must first return to neutral.
            foreach (var card in cards)
                if (card != null && card.gameObject.activeInHierarchy && card.Button.IsActive() && card.Button.IsInteractable())
                { events.SetSelectedGameObject(card.Button.gameObject); break; }
        }

        private void RevealFocus(ExplorationNodeChoiceCardView card)
        {
            if (scrollRect == null || scrollRect.viewport == null || content == null) return;
            var viewport = scrollRect.viewport;
            ((RectTransform)card.transform).GetWorldCorners(focusCorners);
            var bottom = viewport.InverseTransformPoint(focusCorners[0]).y;
            var top = viewport.InverseTransformPoint(focusCorners[1]).y;
            var delta = top > viewport.rect.yMax ? viewport.rect.yMax - top :
                bottom < viewport.rect.yMin ? viewport.rect.yMin - bottom : 0f;
            if (delta == 0f) return;
            scrollRect.StopMovement();
            content.position += viewport.TransformVector(new Vector3(0f, delta, 0f));
        }

        private void OnDisable() => Hide();
        private void OnDestroy() { Hide(); foreach (var card in cards) if (card != null) card.Focused -= RevealFocus; }

#if UNITY_EDITOR
        public void ConfigureWindowServiceForEditor(GameWindowService service) => windowService = service;
        public void ConfigureForEditor(ScrollRect targetScrollRect, RectTransform targetContent,
            ExplorationNodeChoiceCardView targetPrefab, ExplorationNodeChoiceLayoutGroup targetLayout)
        { scrollRect = targetScrollRect; content = targetContent; cardPrefab = targetPrefab; layout = targetLayout; }
#endif
    }
}
