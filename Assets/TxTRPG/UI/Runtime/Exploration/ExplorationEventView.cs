using System;
using System.Collections.Generic;
using TMPro;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    public sealed class ExplorationEventChoiceDisplay
    {
        public ExplorationEventChoiceDisplay(string label, bool isEnabled, string reason)
        {
            Label = label ?? string.Empty; IsEnabled = isEnabled; Reason = reason ?? string.Empty;
        }

        public string Label { get; }
        public bool IsEnabled { get; }
        // Shown under a disabled choice, so the state is not told by color alone.
        public string Reason { get; }
    }

    // One page of an exploration event as display text. Every string is final; the view formats nothing.
    public sealed class ExplorationEventPageDisplay
    {
        public ExplorationEventPageDisplay(string title, string body, IReadOnlyList<ExplorationEventChoiceDisplay> choices, bool isBusy = false)
        {
            Title = title ?? string.Empty; Body = body ?? string.Empty;
            Choices = choices ?? Array.Empty<ExplorationEventChoiceDisplay>();
            IsBusy = isBusy;
        }

        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<ExplorationEventChoiceDisplay> Choices { get; }
        // True while a result is being decided (every choice is locked).
        public bool IsBusy { get; }
    }

    // The event screen inside ExplorationNodePanel: title, scrolling body and a choice list built from one template. A
    // new page never selects a choice by itself; the first direction input after a neutral state focuses the first enabled
    // choice, like the node cards. One click or Submit locks every choice until the next page is shown. Cancel does
    // nothing here. While a game window blocks gameplay input, choices cannot be used and focus is released.
    [DisallowMultipleComponent]
    public sealed class ExplorationEventView : MonoBehaviour
    {
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private ScrollRect bodyScroll;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private ScrollRect choicesScroll;
        [SerializeField] private LayoutElement choicesLayout;
        [SerializeField] private RectTransform choicesContent;
        [SerializeField] private ExplorationEventChoiceView choiceTemplate;
        [SerializeField] private GameWindowService windowService;
        [SerializeField, Range(.2f, .8f), Tooltip("Largest share of the view height the choice list takes before it scrolls.")]
        private float maximumChoicesShare = .5f;
        [SerializeField, Min(48f)] private float minimumChoicesHeight = 56f;

        private readonly List<ExplorationEventChoiceView> choices = new();
        private Action<int> onChoice;
        private bool showing, locked, navigationArmed;
        private int generation;

        public bool IsShowing => showing;
        public bool IsLocked => locked;
        public int Generation => generation;
        public string TitleText => titleText != null ? titleText.text : string.Empty;
        public string BodyText => bodyText != null ? bodyText.text : string.Empty;
        public IReadOnlyList<ExplorationEventChoiceView> Choices => choices;
        public GameWindowService WindowService => windowService;
        private bool InputBlocked => windowService != null && windowService.BlocksGameplayInput;

        public void Show(ExplorationEventPageDisplay page, Action<int> choiceSelected)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (choiceTemplate == null || choicesContent == null) throw new InvalidOperationException("Exploration event view references are incomplete.");
            ClearOwnedFocus();
            generation++;
            navigationArmed = false;
            onChoice = choiceSelected;
            gameObject.SetActive(true);
            showing = true;
            locked = page.IsBusy;
            SetText(titleText, page.Title);
            SetText(bodyText, page.Body);
            choiceTemplate.gameObject.SetActive(false);
            while (choices.Count < page.Choices.Count)
            {
                var view = Instantiate(choiceTemplate, choicesContent);
                view.name = $"Choice{choices.Count + 1}";
                choices.Add(view);
            }
            for (var i = 0; i < choices.Count; i++)
            {
                if (i >= page.Choices.Count) { choices[i].Unbind(); choices[i].gameObject.SetActive(false); continue; }
                var index = i;
                var currentGeneration = generation;
                choices[i].gameObject.SetActive(true);
                choices[i].Bind(page.Choices[i], !locked, () => Request(index, currentGeneration));
            }
            RefreshChoicesHeight();
            if (bodyScroll != null && bodyScroll.content != null) bodyScroll.verticalNormalizedPosition = 1f;
            if (choicesScroll != null && choicesScroll.content != null) choicesScroll.verticalNormalizedPosition = 1f;
        }

        public void Hide()
        {
            ClearOwnedFocus();
            generation++;
            showing = locked = navigationArmed = false;
            onChoice = null;
            foreach (var choice in choices) if (choice != null) choice.Unbind();
            gameObject.SetActive(false);
        }

        // Locks every choice (for example while the result of a choice is applied).
        public void Lock()
        {
            locked = true;
            foreach (var choice in choices) if (choice != null) choice.SetInteractable(false);
        }

        private void Request(int index, int requestGeneration)
        {
            if (!showing || locked || InputBlocked || requestGeneration != generation || !isActiveAndEnabled) return;
            if (index < 0 || index >= choices.Count || !choices[index].IsEnabled) return;
            Lock();
            onChoice?.Invoke(index);
        }

        // Called on page change and when the view size changes, never per frame.
        private void RefreshChoicesHeight()
        {
            if (choicesLayout == null || choicesContent == null || visualRoot == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(choicesContent);
            var content = LayoutUtility.GetPreferredHeight(choicesContent);
            var limit = Mathf.Max(minimumChoicesHeight, visualRoot.rect.height * maximumChoicesShare);
            var height = Mathf.Clamp(content, minimumChoicesHeight, limit);
            if (!Mathf.Approximately(choicesLayout.preferredHeight, height)) choicesLayout.preferredHeight = height;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (showing && isActiveAndEnabled) RefreshChoicesHeight();
        }

        private void ClearOwnedFocus()
        {
            var events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null) return;
            foreach (var choice in choices)
                if (choice != null && events.currentSelectedGameObject == choice.Button.gameObject) { events.SetSelectedGameObject(null); return; }
        }

        private void Update()
        {
            if (InputBlocked) { navigationArmed = false; ClearOwnedFocus(); }
        }

        // Same entry rule as the node cards: after all EventSystem processing of this frame, so the entry input neither
        // moves twice nor submits.
        private void LateUpdate()
        {
            var events = EventSystem.current;
            var module = events != null ? events.currentInputModule as InputSystemUIInputModule : null;
            var move = module != null ? module.move?.action : null;
            if (!showing || locked || InputBlocked || events == null || !events.sendNavigationEvents ||
                module == null || !module.isActiveAndEnabled || move == null || !move.enabled)
            { navigationArmed = false; return; }
            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) { navigationArmed = false; return; }
            if (move.ReadValue<Vector2>().sqrMagnitude < .0001f) { navigationArmed = true; return; }
            if (!navigationArmed) return;
            navigationArmed = false;
            foreach (var choice in choices)
                if (choice != null && choice.gameObject.activeInHierarchy && choice.Button.IsActive() && choice.Button.IsInteractable())
                { events.SetSelectedGameObject(choice.Button.gameObject); break; }
        }

        private void OnDisable()
        {
            ClearOwnedFocus();
            navigationArmed = false;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null && text.text != value) text.text = value;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetVisualRoot, TMP_Text targetTitle, ScrollRect targetBodyScroll, TMP_Text targetBody,
            ScrollRect targetChoicesScroll, LayoutElement targetChoicesLayout, RectTransform targetChoicesContent, ExplorationEventChoiceView targetTemplate)
        {
            visualRoot = targetVisualRoot; titleText = targetTitle; bodyScroll = targetBodyScroll; bodyText = targetBody;
            choicesScroll = targetChoicesScroll; choicesLayout = targetChoicesLayout; choicesContent = targetChoicesContent; choiceTemplate = targetTemplate;
        }

        public void ConfigureWindowServiceForEditor(GameWindowService service) => windowService = service;
        public ExplorationEventChoiceView ChoiceTemplateForEditor => choiceTemplate;
        public void RequestForEditor(int index) => Request(index, generation);
#endif
    }
}
