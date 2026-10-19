using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Encounters
{
    // Display data for one encounter element toggle button.
    public readonly struct EncounterElementToggleEntry
    {
        public EncounterElementToggleEntry(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    // One multi-select toggle button (no ToggleGroup). The root belongs to the bar layout and HitArea is the input target;
    // effects go on VisualRoot. Selection is shown by the Checkmark and the thick SelectedBorder, never by color alone.
    [DisallowMultipleComponent]
    public sealed class EncounterElementToggleView : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private GameObject selectedBorder;
        [SerializeField] private GameObject checkmark;
        [SerializeField] private TMP_Text label;

        private bool listening;

        public event Action<string, bool> ValueChanged;

        public Toggle Toggle => toggle;
        public RectTransform VisualRoot => visualRoot;
        public GameObject SelectedBorder => selectedBorder;
        public GameObject Checkmark => checkmark;
        public TMP_Text Label => label;
        public string Id { get; private set; } = string.Empty;
        public bool IsOn => toggle != null && toggle.isOn;

        public bool IsValid(out string missing)
        {
            missing = toggle == null ? nameof(toggle) : visualRoot == null ? nameof(visualRoot) : selectedBorder == null ? nameof(selectedBorder)
                : checkmark == null ? nameof(checkmark) : label == null ? nameof(label) : string.Empty;
            return missing.Length == 0;
        }

        private void Awake() => Listen();

        private void OnDestroy()
        {
            if (listening && toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
            listening = false;
        }

        // Rebinds a pooled view: clears the selection without notifying.
        public void Bind(EncounterElementToggleEntry entry)
        {
            Listen();
            Id = entry.Id;
            if (label != null) label.text = entry.DisplayName;
            SetIsOnWithoutNotify(false);
        }

        public void SetIsOnWithoutNotify(bool value)
        {
            if (toggle != null) toggle.SetIsOnWithoutNotify(value);
            ApplySelectedVisual(value);
        }

        private void Listen()
        {
            if (listening || toggle == null) return;
            toggle.onValueChanged.AddListener(OnToggleChanged);
            listening = true;
        }

        private void OnToggleChanged(bool value)
        {
            ApplySelectedVisual(value);
            ValueChanged?.Invoke(Id, value);
        }

        private void ApplySelectedVisual(bool value)
        {
            if (selectedBorder != null) selectedBorder.SetActive(value);
            if (checkmark != null) checkmark.SetActive(value);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(Toggle target, RectTransform visual, GameObject border, GameObject check, TMP_Text text)
        { toggle = target; visualRoot = visual; selectedBorder = border; checkmark = check; label = text; }
#endif
    }
}
