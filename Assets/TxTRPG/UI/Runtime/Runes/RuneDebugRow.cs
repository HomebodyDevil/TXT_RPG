using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Runes
{
    // One test rune row of RuneDebugTabView: name, definition ID, count and the −/+ buttons.
    [DisallowMultipleComponent]
    public sealed class RuneDebugRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;

        private bool listening;

        public event Action<string> AddClicked;
        public event Action<string> RemoveClicked;

        public TMP_Text NameText => nameText;
        public TMP_Text IdText => idText;
        public TMP_Text CountText => countText;
        public Button MinusButton => minusButton;
        public Button PlusButton => plusButton;
        public string DefinitionId { get; private set; } = string.Empty;
        public int Count { get; private set; }
        public bool IsValid => nameText != null && idText != null && countText != null && minusButton != null && plusButton != null;

        public void Show(RuneDebugEntryDisplay entry)
        {
            Listen();
            DefinitionId = entry.DefinitionId;
            Count = entry.Count;
            SetText(nameText, entry.DisplayName);
            SetText(idText, entry.DefinitionId);
            SetText(countText, RuneDebugTabView.FormatCount(entry.Count));
        }

        public void SetInteractable(bool available)
        {
            if (plusButton != null) plusButton.interactable = available;
            if (minusButton != null) minusButton.interactable = available && Count > 0;
        }

        private void OnDestroy()
        {
            if (!listening) return;
            if (minusButton != null) minusButton.onClick.RemoveListener(OnMinus);
            if (plusButton != null) plusButton.onClick.RemoveListener(OnPlus);
        }

        private void Listen()
        {
            if (listening) return;
            listening = true;
            if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
            if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
        }

        private void OnMinus() => RemoveClicked?.Invoke(DefinitionId);
        private void OnPlus() => AddClicked?.Invoke(DefinitionId);

        private static void SetText(TMP_Text text, string value) { if (text != null && text.text != value) text.text = value; }

#if UNITY_EDITOR
        public void ConfigureForEditor(TMP_Text title, TMP_Text id, TMP_Text count, Button minus, Button plus)
        {
            nameText = title; idText = id; countText = count; minusButton = minus; plusButton = plus;
        }
#endif
    }
}
