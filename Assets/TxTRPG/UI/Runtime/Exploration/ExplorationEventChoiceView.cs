using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    // One event choice: Button root (layout and hit area) > VisualRoot (background, effects only) with the label and the
    // disabled reason as siblings laid out by the root, so effects on VisualRoot never move the text or the hit area.
    [DisallowMultipleComponent]
    public sealed class ExplorationEventChoiceView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text reason;

        private Action clicked;

        public Button Button => button;
        public RectTransform VisualRoot => visualRoot;
        public bool IsEnabled { get; private set; }
        public string LabelText => label != null ? label.text : string.Empty;
        public string ReasonText => reason != null && reason.gameObject.activeSelf ? reason.text : string.Empty;

        public void Bind(ExplorationEventChoiceDisplay display, bool interactable, Action onClick)
        {
            if (display == null) throw new ArgumentNullException(nameof(display));
            Unbind();
            IsEnabled = display.IsEnabled;
            if (label != null && label.text != display.Label) label.text = display.Label;
            var showReason = !display.IsEnabled && display.Reason.Length > 0;
            if (reason != null)
            {
                if (reason.text != display.Reason) reason.text = display.Reason;
                if (reason.gameObject.activeSelf != showReason) reason.gameObject.SetActive(showReason);
            }
            clicked = onClick;
            button.onClick.AddListener(OnClick);
            SetInteractable(interactable);
        }

        public void Unbind()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
            clicked = null;
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value && IsEnabled;
        }

        private void OnClick() => clicked?.Invoke();

        private void OnDestroy() => Unbind();

#if UNITY_EDITOR
        public void ConfigureForEditor(Button targetButton, RectTransform targetVisualRoot, TMP_Text targetLabel, TMP_Text targetReason)
        { button = targetButton; visualRoot = targetVisualRoot; label = targetLabel; reason = targetReason; }
#endif
    }
}
