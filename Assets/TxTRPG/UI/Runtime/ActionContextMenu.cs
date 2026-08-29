using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public sealed class ActionContextMenu : MonoBehaviour
    {
        [SerializeField] private RectTransform optionList;
        [SerializeField] private Button optionPrefab;

        private readonly List<Button> optionPool = new();
        private GameObject focusReturnTarget;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (optionList == null)
            {
                return;
            }

            var existingButtons = optionList.GetComponentsInChildren<Button>(true);
            foreach (var button in existingButtons)
            {
                if (button != null && button != optionPrefab && !optionPool.Contains(button))
                {
                    optionPool.Add(button);
                }
            }
        }

        public void Show(
            IReadOnlyList<ActionMenuOption> options,
            Action<ActionMenuOption> onSelected,
            GameObject returnFocusTo)
        {
            focusReturnTarget = returnFocusTo;
            var count = options?.Count ?? 0;
            EnsurePool(count);
            for (var i = 0; i < optionPool.Count; i++)
            {
                var button = optionPool[i];
                var active = i < count;
                button.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var option = options[i];
                button.interactable = option.IsEnabled;
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = option.IsEnabled || string.IsNullOrEmpty(option.DisabledReason)
                        ? option.Label
                        : $"{option.Label} ({option.DisabledReason})";
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onSelected?.Invoke(option));
            }

            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var first = optionPool.Find(candidate => candidate.gameObject.activeSelf && candidate.interactable);
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        public void Hide(bool restoreFocus = true)
        {
            gameObject.SetActive(false);
            if (restoreFocus && focusReturnTarget != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(focusReturnTarget);
            }
        }

        private void EnsurePool(int count)
        {
            while (optionPool.Count < count)
            {
                var button = Instantiate(optionPrefab, optionList);
                button.gameObject.SetActive(false);
                optionPool.Add(button);
            }
        }
    }
}
