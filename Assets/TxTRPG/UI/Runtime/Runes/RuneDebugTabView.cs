using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Runes
{
    // Display data of one test rune row: definition ID, name and the count owned in the session.
    public readonly struct RuneDebugEntryDisplay
    {
        public RuneDebugEntryDisplay(string definitionId, string displayName, int count)
        {
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Count = Mathf.Max(0, count);
        }

        public string DefinitionId { get; }
        public string DisplayName { get; }
        public int Count { get; }
    }

    // Rune tab of the development debug panel: 모두 제거 at the top, one row per test rune (name, definition ID, count,
    // −/+) and the last result. It knows nothing about runes or the session: the owner listens to the events and calls the
    // Set methods, which never raise events. Rows are clones of an inactive template and are reused. The page is never
    // shorter than the reference page (the encounter page), like the slot tab.
    [DisallowMultipleComponent]
    public sealed class RuneDebugTabView : MonoBehaviour
    {
        public const string NoDefinitionsMessage = "테스트 룬 정의가 없습니다";

        [SerializeField] private Button removeAllButton;
        [SerializeField] private RectTransform rowsRoot;
        [SerializeField, Tooltip("Inactive template row; cloned for each test rune.")] private RuneDebugRow rowTemplate;
        [SerializeField] private TMP_Text resultText;
        [SerializeField, Tooltip("Height of the page; never below the reference page height.")] private LayoutElement pageLayout;
        [SerializeField] private RectTransform heightReference;
        [SerializeField, Tooltip("Up navigation target of 모두 제거 (the selected tab button).")] private Selectable navigationUp;

        private readonly List<RuneDebugRow> rows = new();
        private int activeRowCount;
        private bool listening;
        private bool sessionAvailable = true;
        private float laidOutReference = -1f;

        public event Action<string> AddRequested;
        public event Action<string> RemoveRequested;
        public event Action RemoveAllRequested;

        public Button RemoveAllButton => removeAllButton;
        public TMP_Text ResultText => resultText;
        public RuneDebugRow RowTemplate => rowTemplate;
        public int ActiveRowCount => activeRowCount;
        public bool IsSessionAvailable => sessionAvailable;
        public RectTransform HeightReference => heightReference;

        public RuneDebugRow GetRow(int index) => index >= 0 && index < activeRowCount ? rows[index] : null;

        public bool IsValid(out string missing)
        {
            missing = removeAllButton == null ? nameof(removeAllButton) : rowsRoot == null ? nameof(rowsRoot)
                : rowTemplate == null || !rowTemplate.IsValid ? nameof(rowTemplate) : resultText == null ? nameof(resultText) : string.Empty;
            return missing.Length == 0;
        }

        private void Awake() => Listen();

        private void LateUpdate()
        {
            var reference = heightReference != null && heightReference != transform ? heightReference.rect.height : 0f;
            if (Mathf.Approximately(reference, laidOutReference) || pageLayout == null) return;
            laidOutReference = reference;
            if (reference > 0f && !Mathf.Approximately(pageLayout.minHeight, reference)) pageLayout.minHeight = reference;
        }

        private void OnDestroy()
        {
            if (!listening) return;
            if (removeAllButton != null) removeAllButton.onClick.RemoveListener(OnRemoveAll);
            listening = false;
        }

        // Shows the test runes without raising events. Rows are reused; navigation is rebuilt when the row count changes.
        public void SetEntries(IReadOnlyList<RuneDebugEntryDisplay> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Listen();
            var countChanged = entries.Count != activeRowCount;
            if (countChanged) LayoutRows(entries.Count);
            for (var i = 0; i < entries.Count; i++) rows[i].Show(entries[i]);
            if (countChanged) RefreshNavigation();
            RefreshInteractable();
        }

        public void SetSessionAvailable(bool available)
        {
            Listen();
            sessionAvailable = available;
            RefreshInteractable();
        }

        public void ShowResult(string message)
        {
            Listen();
            if (resultText != null && resultText.text != (message ?? string.Empty)) resultText.text = message ?? string.Empty;
        }

        private void LayoutRows(int count)
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var lostFocus = false;
            while (rows.Count < count) AddRow();
            for (var i = 0; i < rows.Count; i++)
            {
                var active = i < count;
                if (!active && selected != null && selected.transform.IsChildOf(rows[i].transform)) lostFocus = true;
                if (rows[i].gameObject.activeSelf != active) rows[i].gameObject.SetActive(active);
            }
            activeRowCount = count;
            if (lostFocus && removeAllButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(removeAllButton.gameObject);
        }

        private void AddRow()
        {
            if (rowTemplate == null || rowsRoot == null) throw new InvalidOperationException($"{name}: the row template is missing.");
            var row = Instantiate(rowTemplate, rowsRoot, false);
            row.name = $"RuneRow{rows.Count + 1}";
            row.AddClicked += id => { if (sessionAvailable) AddRequested?.Invoke(id); };
            row.RemoveClicked += id => { if (sessionAvailable) RemoveRequested?.Invoke(id); };
            rows.Add(row);
        }

        private void Listen()
        {
            if (listening) return;
            listening = true;
            if (rowTemplate != null && rowTemplate.gameObject.activeSelf) rowTemplate.gameObject.SetActive(false);
            if (removeAllButton != null) removeAllButton.onClick.AddListener(OnRemoveAll);
            RefreshNavigation();
            RefreshInteractable();
        }

        private void OnRemoveAll() { if (sessionAvailable) RemoveAllRequested?.Invoke(); }

        private void RefreshInteractable()
        {
            if (removeAllButton != null) removeAllButton.interactable = sessionAvailable && activeRowCount > 0;
            for (var i = 0; i < activeRowCount; i++) rows[i].SetInteractable(sessionAvailable);
        }

        // Explicit chain: tab → 모두 제거 → each row (− ↔ +) → down to the next row.
        private void RefreshNavigation()
        {
            Selectable firstRowMinus = activeRowCount > 0 ? rows[0].MinusButton : null;
            SetNavigation(removeAllButton, null, null, navigationUp, firstRowMinus);
            for (var i = 0; i < activeRowCount; i++)
            {
                var row = rows[i];
                var upMinus = i == 0 ? (Selectable)removeAllButton : rows[i - 1].MinusButton;
                var upPlus = i == 0 ? (Selectable)removeAllButton : rows[i - 1].PlusButton;
                var downMinus = i + 1 < activeRowCount ? rows[i + 1].MinusButton : null;
                var downPlus = i + 1 < activeRowCount ? rows[i + 1].PlusButton : null;
                SetNavigation(row.MinusButton, null, row.PlusButton, upMinus, downMinus);
                SetNavigation(row.PlusButton, row.MinusButton, null, upPlus, downPlus);
            }
        }

        private static void SetNavigation(Selectable target, Selectable left, Selectable right, Selectable up, Selectable down)
        {
            if (target == null) return;
            target.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
                selectOnUp = up,
                selectOnDown = down
            };
        }

        public static string FormatCount(int count) => Mathf.Max(0, count).ToString(CultureInfo.InvariantCulture);

#if UNITY_EDITOR
        public void ConfigureForEditor(Button removeAll, RectTransform root, RuneDebugRow template, TMP_Text result,
            LayoutElement page, RectTransform reference, Selectable up)
        {
            removeAllButton = removeAll; rowsRoot = root; rowTemplate = template; resultText = result; pageLayout = page;
            heightReference = reference; navigationUp = up;
        }
#endif
    }
}
