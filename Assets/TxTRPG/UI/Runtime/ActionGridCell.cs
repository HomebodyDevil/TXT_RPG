using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class ActionGridCell : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI quantity;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private GameObject disabledOverlay;
        [SerializeField] private GameObject selectionFrame;
        [SerializeField] private TextMeshProUGUI shortcutLabel;
        [SerializeField] private GameObject emptySlotVisual;

        private Action<int> activate;

        public int Index { get; private set; }
        public bool HasEntry { get; private set; }
        public Button Button => button;

        private void Awake()
        {
            button?.onClick.AddListener(Activate);
        }

        public void Bind(int index, in ActionGridEntry entry, Action<int> onActivate)
        {
            Index = index;
            HasEntry = true;
            activate = onActivate;
            gameObject.name = $"ActionGridCell {index}: {entry.DisplayName}";

            if (icon != null)
            {
                icon.sprite = entry.Icon;
                icon.enabled = entry.Icon != null;
            }

            if (quantity != null)
            {
                quantity.text = entry.Quantity > 1 ? entry.Quantity.ToString() : string.Empty;
                quantity.gameObject.SetActive(entry.Quantity > 1);
            }

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = entry.CooldownNormalized;
                cooldownOverlay.gameObject.SetActive(entry.CooldownNormalized > 0f);
            }

            disabledOverlay?.SetActive(!entry.IsEnabled);
            emptySlotVisual?.SetActive(false);
            if (shortcutLabel != null)
            {
                shortcutLabel.text = entry.ShortcutLabel;
                shortcutLabel.gameObject.SetActive(!string.IsNullOrEmpty(entry.ShortcutLabel));
            }

            if (button != null)
            {
                button.interactable = true;
            }

            SetSelected(false);
        }

        public void BindEmpty(int index, Action<int> onActivate)
        {
            Index = index;
            HasEntry = false;
            activate = onActivate;
            gameObject.name = $"ActionGridCell {index}: Empty";
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            quantity?.gameObject.SetActive(false);
            cooldownOverlay?.gameObject.SetActive(false);
            disabledOverlay?.SetActive(false);
            shortcutLabel?.gameObject.SetActive(false);
            emptySlotVisual?.SetActive(true);
            if (button != null)
            {
                button.interactable = true;
            }

            SetSelected(false);
        }

        public void Unbind()
        {
            activate = null;
            HasEntry = false;
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (quantity != null) { quantity.text = string.Empty; quantity.gameObject.SetActive(false); }
            if (cooldownOverlay != null) { cooldownOverlay.fillAmount = 0f; cooldownOverlay.gameObject.SetActive(false); }
            disabledOverlay?.SetActive(false);
            selectionFrame?.SetActive(false);
            if (shortcutLabel != null) { shortcutLabel.text = string.Empty; shortcutLabel.gameObject.SetActive(false); }
            emptySlotVisual?.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            selectionFrame?.SetActive(selected);
        }

        public void ClearIcon()
        {
            if (icon == null) return;
            icon.sprite = null;
            icon.enabled = false;
        }

        private void Activate()
        {
            activate?.Invoke(Index);
        }
    }
}
