using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.DebugPanels
{
    [Serializable]
    public sealed class DebugPanelTab
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField, Tooltip("Shown only while this tab is selected (shape, not only color).")]
        private GameObject selectedIndicator;
        [SerializeField, Tooltip("Active only while this tab is selected.")] private GameObject page;
        [SerializeField, Tooltip("Down navigation target of the tab buttons while this page is shown.")]
        private Selectable firstControl;

        public DebugPanelTab() { }

        public DebugPanelTab(Button tabButton, TMP_Text tabLabel, GameObject indicator, GameObject tabPage, Selectable first)
        {
            button = tabButton;
            label = tabLabel;
            selectedIndicator = indicator;
            page = tabPage;
            firstControl = first;
        }

        public Button Button => button;
        public TMP_Text Label => label;
        public GameObject SelectedIndicator => selectedIndicator;
        public GameObject Page => page;
        public Selectable FirstControl => firstControl;
    }

    // Tabs of a development panel: one button per tab and one page per tab, only the selected page active (inactive
    // pages drop out of navigation and raycasts). It knows nothing about the pages' content. The selection is marked by
    // an indicator object and a bold label. Clicking a tab keeps the focus on that tab button; the panel never takes the
    // focus by itself. The selection lives for the Scene only.
    [DisallowMultipleComponent]
    public sealed class DebugPanelTabGroup : MonoBehaviour
    {
        [SerializeField] private List<DebugPanelTab> tabs = new();
        [SerializeField, Min(0)] private int initialIndex;

        private readonly List<UnityEngine.Events.UnityAction> clickHandlers = new();
        private bool initialized;
        private int selectedIndex = -1;

        public event Action<int> SelectionChanged;

        public int SelectedIndex { get { EnsureInitialized(); return selectedIndex; } }
        public int TabCount => tabs.Count;
        public IReadOnlyList<DebugPanelTab> Tabs => tabs;

        public bool IsValid(out string missing)
        {
            if (tabs.Count == 0) { missing = nameof(tabs); return false; }
            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                missing = tab == null ? $"tabs[{i}]" : tab.Button == null ? $"tabs[{i}].button" : tab.Label == null ? $"tabs[{i}].label"
                    : tab.SelectedIndicator == null ? $"tabs[{i}].selectedIndicator" : tab.Page == null ? $"tabs[{i}].page" : string.Empty;
                if (missing.Length > 0) return false;
            }
            missing = string.Empty;
            return true;
        }

        private void Awake() => EnsureInitialized();

        private void OnDestroy()
        {
            for (var i = 0; i < clickHandlers.Count && i < tabs.Count; i++)
                if (tabs[i]?.Button != null) tabs[i].Button.onClick.RemoveListener(clickHandlers[i]);
            clickHandlers.Clear();
            initialized = false;
        }

        // Selects a tab; the same tab does nothing, an index outside the tabs is ignored with a warning.
        public void Select(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= tabs.Count)
            {
                Debug.LogWarning($"{name}: tab index {index} is outside 0..{tabs.Count - 1}.", this);
                return;
            }
            if (index == selectedIndex) return;
            Apply(index);
            SelectionChanged?.Invoke(index);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            clickHandlers.Clear();
            for (var i = 0; i < tabs.Count; i++)
            {
                var index = i;
                UnityEngine.Events.UnityAction handler = () => Select(index);
                clickHandlers.Add(handler);
                if (tabs[i]?.Button != null) tabs[i].Button.onClick.AddListener(handler);
            }
            if (tabs.Count > 0) Apply(Mathf.Clamp(initialIndex, 0, tabs.Count - 1));
        }

        private void Apply(int index)
        {
            selectedIndex = index;
            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                if (tab == null) continue;
                var selected = i == index;
                if (tab.Page != null && tab.Page.activeSelf != selected) tab.Page.SetActive(selected);
                if (tab.SelectedIndicator != null && tab.SelectedIndicator.activeSelf != selected) tab.SelectedIndicator.SetActive(selected);
                if (tab.Label != null)
                    tab.Label.fontStyle = selected ? tab.Label.fontStyle | FontStyles.Bold : tab.Label.fontStyle & ~FontStyles.Bold;
            }
            RefreshNavigation();
        }

        // Tab buttons: left/right to the neighbouring tabs, down to the selected page's first control.
        private void RefreshNavigation()
        {
            var down = selectedIndex >= 0 && selectedIndex < tabs.Count ? tabs[selectedIndex]?.FirstControl : null;
            for (var i = 0; i < tabs.Count; i++)
            {
                var button = tabs[i]?.Button;
                if (button == null) continue;
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? tabs[i - 1]?.Button : null,
                    selectOnRight = i + 1 < tabs.Count ? tabs[i + 1]?.Button : null,
                    selectOnUp = button.navigation.selectOnUp,
                    selectOnDown = down
                };
                button.navigation = navigation;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<DebugPanelTab> entries, int initial)
        {
            tabs = new List<DebugPanelTab>(entries ?? Array.Empty<DebugPanelTab>());
            initialIndex = Mathf.Max(0, initial);
            initialized = false;
            selectedIndex = -1;
        }
#endif
    }
}
