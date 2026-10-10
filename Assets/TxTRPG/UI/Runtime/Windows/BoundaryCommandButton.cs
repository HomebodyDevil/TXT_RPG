using TxTRPG.UI.Exploration;
using TxTRPG.UI.Shapes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    // A command button overlaid on a panel boundary (see PanelBoundaryPlacement). It executes through the same
    // IGameMenuCommandHandler contract as the game menu, so the UI assembly never references gameplay code.
    // The owner decides availability (SetAvailable) and requests refreshes; nothing here polls.
    [DisallowMultipleComponent]
    public sealed class BoundaryCommandButton : MonoBehaviour
    {
        [SerializeField] private PanelBoundaryPlacement placement;
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private Button button;
        [SerializeField] private ShapedButtonView shapedView;
        [SerializeField] private MonoBehaviour commandHandlerBehaviour;
        [SerializeField] private string commandId = string.Empty;
        [SerializeField, Tooltip("Selected when the button hides while it has focus. Empty clears the selection " +
            "(the exploration screen starts without focus and the first Navigate input enters its cards).")]
        private Selectable focusFallback;
        [SerializeField, Tooltip("Follow the player's card effect / Reduce Motion settings for animated button effects.")]
        private bool followMotionPreferences = true;

        private bool available;
        private bool listening;
        // Unity clears the selection when a selected Selectable becomes non-interactable (during a roll). Remember it so
        // focus returns here once the button is usable again and nothing else visible took focus (e.g. the result window closed).
        private bool restoreFocusWhenEnabled;

        public bool IsAvailable => available;
        public bool IsShown => buttonRoot != null && buttonRoot.activeSelf;
        public bool IsInteractable => button != null && button.interactable;
        public string CommandId => commandId?.Trim() ?? string.Empty;
        public string UnavailableReason { get; private set; } = string.Empty;
        public Button Button => button;
        public ShapedButtonView ShapedView => shapedView;
        public PanelBoundaryPlacement Placement => placement;
        public Selectable FocusFallback => focusFallback;
        private IGameMenuCommandHandler CommandHandler => commandHandlerBehaviour as IGameMenuCommandHandler;

        private void OnEnable() => Bind();
        private void Start() => Refresh();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (listening) return;
            listening = true;
            if (placement != null) placement.BoundaryChanged += UpdateVisibility;
            if (button != null) button.onClick.AddListener(OnClicked);
            if (followMotionPreferences) ExplorationCardPlayerPreferences.Changed += ApplyMotionPreference;
            ApplyMotionPreference();
        }

        private void Unbind()
        {
            if (!listening) return;
            listening = false;
            if (placement != null) placement.BoundaryChanged -= UpdateVisibility;
            if (button != null) button.onClick.RemoveListener(OnClicked);
            ExplorationCardPlayerPreferences.Changed -= ApplyMotionPreference;
        }

        public void SetAvailable(bool value)
        {
            available = value;
            Refresh();
        }

        public void Refresh()
        {
            UpdateVisibility();
            RefreshExecutionState();
        }

        public void RefreshExecutionState()
        {
            if (button == null) return;
            var handler = CommandHandler;
            var reason = string.Empty;
            var executable = available && handler != null && handler.CanExecute(CommandId, out reason);
            UnavailableReason = executable ? string.Empty : !available ? "The command is not available now." :
                handler == null ? "No command handler is connected." : reason;
            var eventSystem = EventSystem.current;
            if (!executable && button.interactable && eventSystem != null && eventSystem.currentSelectedGameObject == button.gameObject)
                restoreFocusWhenEnabled = true;
            button.interactable = executable;
            if (shapedView != null) shapedView.SetInteractableVisual(executable);
            if (!executable || !restoreFocusWhenEnabled || !IsShown) return;
            restoreFocusWhenEnabled = false;
            var current = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (eventSystem != null && (current == null || !current.activeInHierarchy)) eventSystem.SetSelectedGameObject(button.gameObject);
        }

        private void OnClicked() => Execute();

        public bool Execute()
        {
            var handler = CommandHandler;
            var reason = string.Empty;
            if (!available || handler == null || !handler.CanExecute(CommandId, out reason))
            {
                UnavailableReason = string.IsNullOrWhiteSpace(reason) ? "The command is not ready." : reason;
                Debug.LogWarning($"Cannot execute boundary command '{CommandId}': {UnavailableReason}", this);
                RefreshExecutionState();
                return false;
            }
            return handler.TryExecute(CommandId);
        }

        private void UpdateVisibility()
        {
            if (buttonRoot == null) return;
            var show = available && CommandHandler != null && placement != null && placement.HasBoundary;
            if (buttonRoot.activeSelf == show) return;
            if (!show)
            {
                ReleaseFocus();
                if (shapedView != null) shapedView.ResetEffects();
            }
            buttonRoot.SetActive(show);
            if (show && placement != null) placement.ForceUpdate();
        }

        private void ReleaseFocus()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var pending = restoreFocusWhenEnabled;
            restoreFocusWhenEnabled = false;
            if (eventSystem == null) return;
            // Also covers focus this button lost during a roll and that was left on a now-hidden window control.
            var ownsFocus = selected != null && selected.transform.IsChildOf(buttonRoot.transform);
            if (!ownsFocus && !(pending && selected != null && !selected.activeInHierarchy)) return;
            var fallback = focusFallback != null && focusFallback.isActiveAndEnabled && focusFallback.IsInteractable()
                ? focusFallback.gameObject : null;
            eventSystem.SetSelectedGameObject(fallback);
        }

        private void ApplyMotionPreference()
        {
            if (shapedView != null) shapedView.SetEffectsAllowed(!followMotionPreferences || ExplorationCardPlayerPreferences.AllowsMotion);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(PanelBoundaryPlacement targetPlacement, GameObject targetRoot, Button targetButton,
            ShapedButtonView targetView, MonoBehaviour handler, string targetCommandId, Selectable fallback)
        {
            var wasListening = listening;
            Unbind();
            placement = targetPlacement; buttonRoot = targetRoot; button = targetButton; shapedView = targetView;
            commandHandlerBehaviour = handler; commandId = targetCommandId; focusFallback = fallback;
            if (wasListening) Bind();
        }

        // Edit Mode tests only: this component is runtime-only, so its OnEnable does not run outside Play Mode.
        public void BindForEditorTests() => Bind();
        public void UnbindForEditorTests() => Unbind();
#endif
    }
}
