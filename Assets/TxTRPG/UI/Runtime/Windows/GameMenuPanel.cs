using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public enum GameMenuButtonActionKind { OpenPage = 0, Command = 1 }

    public interface IGameMenuCommandHandler
    {
        bool CanExecute(string commandId, out string unavailableReason);
        bool TryExecute(string commandId);
    }
    [Serializable]
    public sealed class GameMenuButtonBinding
    {
        [SerializeField] private string pageId = string.Empty;
        [SerializeField] private Button button;
        [SerializeField] private bool visible = true;
        [SerializeField] private GameMenuButtonView view;
        [SerializeField] private bool overrideRequestPresentation;
        [SerializeField] private ModalContentKind contentKind = ModalContentKind.CustomContent;
        [SerializeField] private string requestTitle = string.Empty;
        [SerializeField] private GameMenuButtonActionKind actionKind;
        [SerializeField] private string commandId = string.Empty;
        public string PageId => pageId?.Trim() ?? string.Empty;
        public Button Button => button != null ? button : view != null ? view.Button : null;
        public bool Visible => visible;
        public GameMenuButtonView View => view;
        public ModalContentKind ContentKind => contentKind;
        public bool OverridesRequestPresentation => overrideRequestPresentation;
        public string RequestTitle => requestTitle?.Trim() ?? string.Empty;
        public GameMenuButtonActionKind ActionKind => actionKind;
        public string CommandId => commandId?.Trim() ?? string.Empty;
        public void SetVisible(bool value) => visible = value;
#if UNITY_EDITOR
        public void ConfigureForEditor(string id, Button target, bool isVisible = true,
            GameMenuButtonView targetView = null, ModalContentKind kind = ModalContentKind.CustomContent,
            string title = null, bool overridePresentation = false)
        { pageId = id; button = target; visible = isVisible; view = targetView; contentKind = kind;
          requestTitle = title ?? string.Empty; overrideRequestPresentation = overridePresentation; }
        public void ConfigureCommandForEditor(string id, Button target, bool isVisible = true, GameMenuButtonView targetView = null)
        { pageId = string.Empty; commandId = id; actionKind = GameMenuButtonActionKind.Command; button = target; visible = isVisible; view = targetView; }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class GameMenuPanel : MonoBehaviour
    {
        [SerializeField] private List<GameMenuButtonBinding> buttons = new();
        [SerializeField] private GameWindowService windowService;
        [SerializeField] private MonoBehaviour commandHandlerBehaviour;
        [SerializeField] private ActionGridPanel quickItemGrid;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private GameMenuLayoutGroup layout;
        [SerializeField] private Scrollbar horizontalScrollbar;
        [SerializeField] private GameMenuScrollbarVisibility scrollbarVisibility = GameMenuScrollbarVisibility.Hidden;
        [SerializeField] private GameMenuScrollbarSpaceMode scrollbarSpaceMode = GameMenuScrollbarSpaceMode.Overlay;
        [SerializeField, Min(0f)] private float scrollbarHeight = 12f;
        [SerializeField, Min(0f)] private float scrollbarGap = 4f;

        private readonly Dictionary<Button, UnityAction> runtimeListeners = new();
        private readonly HashSet<Button> duplicateGuard = new();
        private bool refreshQueued;
        private bool warnedInsufficientSpace;
        private GameObject lastSelection;

        public GameMenuLayoutResult CurrentLayout => layout != null ? layout.Current : default;
        public bool LayoutInsufficientSpace => CurrentLayout.HasInsufficientSpace;
        public IReadOnlyList<GameMenuButtonBinding> Buttons => buttons;
        public GameWindowService WindowService => windowService;
        public ActionGridPanel QuickItemGrid => quickItemGrid;
        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public ScrollRect ScrollRect => scrollRect;
        public GameMenuLayoutGroup Layout => layout;
        public Scrollbar HorizontalScrollbar => horizontalScrollbar;
        public bool HasValidInternalConfiguration => ValidateInternalConfiguration(out _);
        public bool IsReady => HasValidInternalConfiguration && windowService != null;
        public string UnavailableReason { get; private set; } = string.Empty;
        private IGameMenuCommandHandler CommandHandler => commandHandlerBehaviour as IGameMenuCommandHandler;

        private void OnEnable() { BindButtons(); RefreshExecutionState(); QueueRefresh(); }
        private void Start() { BindButtons(); RefreshExecutionState(); QueueRefresh(); }
        private void OnDisable() => UnbindButtons();
        private void OnRectTransformDimensionsChange() => QueueRefresh();
        private void LateUpdate()
        {
            if (refreshQueued)
            {
                refreshQueued = false;
                ApplyLayout();
            }
            KeepSelectionVisible();
        }

        public void Open(string pageId)
        {
            quickItemGrid?.CloseContextMenu();
            if (!ValidateInternalConfiguration(out var reason))
            {
                UnavailableReason = reason;
                Debug.LogWarning($"Cannot open game-menu page '{pageId}': {UnavailableReason}", this);
                return;
            }
            if (windowService == null)
            {
                UnavailableReason = "GameWindowService is not connected.";
                Debug.LogWarning($"Cannot open game-menu page '{pageId}': {UnavailableReason}", this);
                return;
            }
            if (!windowService.HasPage(pageId))
            {
                UnavailableReason = $"GameWindowService has no page registered for '{pageId}'.";
                Debug.LogWarning($"Cannot open game-menu page '{pageId}': {UnavailableReason}", this);
                return;
            }
            var request = windowService.CreateRequest(pageId, EventSystem.current?.currentSelectedGameObject);
            var binding = buttons.Find(item => item != null && string.Equals(item.PageId, pageId?.Trim(), StringComparison.Ordinal));
            if (request != null && binding != null && binding.OverridesRequestPresentation)
                request = request.WithPresentation(binding.ContentKind, binding.RequestTitle);
            _ = windowService.OpenAsync(request);
        }

        public bool ExecuteCommand(string commandId)
        {
            quickItemGrid?.CloseContextMenu();
            var handler = CommandHandler;
            var reason = string.Empty;
            if (handler == null || !handler.CanExecute(commandId, out reason))
            {
                UnavailableReason = string.IsNullOrWhiteSpace(reason) ? "Game-menu command is not ready." : reason;
                Debug.LogWarning($"Cannot execute game-menu command '{commandId}': {UnavailableReason}", this);
                return false;
            }
            return handler.TryExecute(commandId);
        }
        public bool SetItemVisible(string pageId, bool visible)
        {
            var normalized = pageId?.Trim() ?? string.Empty;
            var binding = buttons.Find(item => item != null &&
                string.Equals(item.PageId, normalized, StringComparison.Ordinal));
            if (binding == null) return false;
            binding.SetVisible(visible);
            if (binding.Button != null) binding.Button.gameObject.SetActive(visible);
            QueueRefresh();
            return true;
        }

        public void ConfigureLayout(GameMenuLayoutMode mode, Vector2 buttonSize, Vector2 spacing,
            RectOffset padding, GameMenuHorizontalAlignment horizontalAlignment,
            GameMenuVerticalAlignment verticalAlignment,
            GameMenuWrapColumnPolicy columnPolicy = GameMenuWrapColumnPolicy.AutoFit,
            int maximumColumns = 4)
        {
            layout?.Configure(mode, buttonSize, spacing, padding, horizontalAlignment,
                verticalAlignment, columnPolicy, maximumColumns);
            QueueRefresh();
        }

        public void ConfigureScrollbar(GameMenuScrollbarVisibility visibility,
            GameMenuScrollbarSpaceMode spaceMode, float height, float gap)
        {
            scrollbarVisibility = visibility;
            scrollbarSpaceMode = spaceMode;
            scrollbarHeight = Mathf.Max(0f, height);
            scrollbarGap = Mathf.Max(0f, gap);
            QueueRefresh();
        }

        public void RefreshLayout() => QueueRefresh();

        public bool BindExternalDependencies(GameWindowService service, ActionGridPanel grid = null,
            bool replaceExistingService = false)
        {
            if (service == null) return false;
            if (windowService != null && windowService != service && !replaceExistingService) return false;
            windowService = service;
            quickItemGrid = grid;
            RefreshExecutionState();
            return true;
        }

        public void UnbindExternalDependencies(GameWindowService service)
        {
            if (windowService != service) return;
            windowService = null;
            quickItemGrid = null;
            RefreshExecutionState();
        }

        public bool ValidateInternalConfiguration(out string reason)
        {
            if (viewport == null || content == null || scrollRect == null || layout == null || horizontalScrollbar == null)
            { reason = "Required internal UI references are incomplete."; return false; }
            if (scrollRect.viewport != viewport || scrollRect.content != content)
            { reason = "ScrollRect references do not match the menu Viewport and Content."; return false; }
            foreach (var binding in buttons)
                if (binding == null || binding.Button == null || binding.View == null)
                { reason = "One or more menu button references are incomplete."; return false; }
            reason = string.Empty;
            return true;
        }

        public void RefreshExecutionState()
        {
            var internallyValid = ValidateInternalConfiguration(out var reason);
            UnavailableReason = !internallyValid ? reason : windowService == null ? "GameWindowService is not connected." : string.Empty;
            foreach (var binding in buttons)
                if (binding?.Button != null)
                    binding.Button.interactable = internallyValid && CanExecute(binding);
        }

        private void BindButtons()
        {
            UnbindButtons();
            duplicateGuard.Clear();
            foreach (var binding in buttons)
            {
                var button = binding?.Button;
                if (button == null) continue;
                button.gameObject.SetActive(binding.Visible);
                if (!duplicateGuard.Add(button))
                {
                    Debug.LogWarning($"Game menu button '{button.name}' is bound more than once; duplicate binding was ignored.", this);
                    continue;
                }
                var pageId = binding.PageId;
                var commandId = binding.CommandId;
                var actionKind = binding.ActionKind;
                UnityAction listener = actionKind == GameMenuButtonActionKind.Command
                    ? () => ExecuteCommand(commandId)
                    : () => Open(pageId);
                runtimeListeners.Add(button, listener);
                button.onClick.AddListener(listener);
            }
        }

        private bool CanExecute(GameMenuButtonBinding binding)
        {
            if (binding.ActionKind == GameMenuButtonActionKind.Command)
                return CommandHandler != null && CommandHandler.CanExecute(binding.CommandId, out _);
            return windowService != null && windowService.HasPage(binding.PageId);
        }
        private void UnbindButtons()
        {
            foreach (var pair in runtimeListeners)
                if (pair.Key != null) pair.Key.onClick.RemoveListener(pair.Value);
            runtimeListeners.Clear();
        }

        private void QueueRefresh()
        {
            refreshQueued = true;
            if (layout != null)
                LayoutRebuilder.MarkLayoutForRebuild(layout.transform as RectTransform);
        }

        private void ApplyLayout()
        {
            if (layout == null || viewport == null || content == null) return;
            var horizontal = layout.LayoutMode == GameMenuLayoutMode.HorizontalScroll;
            if (scrollRect != null)
            {
                scrollRect.horizontal = horizontal;
                scrollRect.vertical = false;
                if (!horizontal) scrollRect.StopMovement();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var overflow = horizontal && layout.Current.RequiredWidth > viewport.rect.width + 0.01f;
            var showScrollbar = horizontal &&
                GameMenuLayoutCalculator.ShouldShowScrollbar(scrollbarVisibility, overflow);
            if (horizontalScrollbar != null) horizontalScrollbar.gameObject.SetActive(showScrollbar);
            var reserve = horizontal &&
                GameMenuLayoutCalculator.ShouldReserveScrollbarSpace(scrollbarSpaceMode, showScrollbar);
            viewport.offsetMin = new Vector2(viewport.offsetMin.x,
                reserve ? scrollbarHeight + scrollbarGap : 0f);

            if (horizontal)
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(viewport.rect.width, layout.Current.RequiredWidth));
            }
            else
            {
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = Vector2.zero;
                content.offsetMax = Vector2.zero;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            if (layout.Current.HasInsufficientSpace && !warnedInsufficientSpace)
            {
                warnedInsufficientSpace = true;
                Debug.LogWarning($"GameMenuPanel has insufficient layout space. Required " +
                    $"{layout.Current.RequiredWidth:0.#}x{layout.Current.RequiredHeight:0.#}, " +
                    $"viewport {viewport.rect.width:0.#}x{viewport.rect.height:0.#}.", this);
            }
            else if (!layout.Current.HasInsufficientSpace) warnedInsufficientSpace = false;
        }

        private void KeepSelectionVisible()
        {
            if (scrollRect == null || viewport == null || content == null ||
                layout == null || layout.LayoutMode != GameMenuLayoutMode.HorizontalScroll) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == lastSelection) return;
            lastSelection = selected;
            if (selected == null || !selected.transform.IsChildOf(content) ||
                selected.transform is not RectTransform selectedRect) return;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selectedRect);
            var view = viewport.rect;
            var position = content.anchoredPosition;
            if (bounds.size.x > view.width) position.x += view.xMin - bounds.min.x;
            else if (bounds.min.x < view.xMin) position.x += view.xMin - bounds.min.x;
            else if (bounds.max.x > view.xMax) position.x -= bounds.max.x - view.xMax;
            content.anchoredPosition = position;
        }

        private void OnValidate()
        {
            scrollbarHeight = Mathf.Max(0f, scrollbarHeight);
            scrollbarGap = Mathf.Max(0f, scrollbarGap);
            QueueRefresh();
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(GameWindowService service, ActionGridPanel grid,
            IEnumerable<GameMenuButtonBinding> configuredButtons, RectTransform targetViewport = null,
            RectTransform targetContent = null, ScrollRect targetScrollRect = null,
            GameMenuLayoutGroup targetLayout = null, Scrollbar targetScrollbar = null)
        {
            windowService = service;
            quickItemGrid = grid;
            buttons = new List<GameMenuButtonBinding>(configuredButtons);
            viewport = targetViewport;
            content = targetContent;
            scrollRect = targetScrollRect;
            layout = targetLayout;
            horizontalScrollbar = targetScrollbar;
        }
        public void SetCommandHandlerForEditor(MonoBehaviour handler) => commandHandlerBehaviour = handler;
        public void AddOrReplaceCommandForEditor(GameMenuButtonBinding binding)
        {
            buttons.RemoveAll(item => item != null && item.ActionKind == GameMenuButtonActionKind.Command && item.CommandId == binding.CommandId);
            buttons.Add(binding);
        }
#endif
    }
}
