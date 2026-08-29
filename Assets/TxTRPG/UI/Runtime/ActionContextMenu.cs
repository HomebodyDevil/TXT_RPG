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
        [SerializeField] private RectTransform placementBounds;
        [SerializeField, Min(0f)] private float anchorGap = 8f;
        [SerializeField, Min(0f)] private float edgePadding = 8f;

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
            RectTransform anchor,
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
            PositionNextTo(anchor);
            var first = optionPool.Find(candidate => candidate.gameObject.activeSelf && candidate.interactable);
            if (first != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            }
        }

        public void Show(
            IReadOnlyList<ActionMenuOption> options,
            Action<ActionMenuOption> onSelected,
            GameObject returnFocusTo)
        {
            Show(options, onSelected, null, returnFocusTo);
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

        private void PositionNextTo(RectTransform anchor)
        {
            var menuRect = transform as RectTransform;
            var bounds = placementBounds != null ? placementBounds : menuRect?.parent as RectTransform;
            if (anchor == null || menuRect == null || bounds == null)
            {
                return;
            }

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var first = bounds.InverseTransformPoint(corners[0]);
            var anchorMin = new Vector2(first.x, first.y);
            var anchorMax = anchorMin;
            for (var i = 1; i < corners.Length; i++)
            {
                var point = bounds.InverseTransformPoint(corners[i]);
                anchorMin = Vector2.Min(anchorMin, point);
                anchorMax = Vector2.Max(anchorMax, point);
            }

            var anchorRect = Rect.MinMaxRect(anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
            var menuSize = menuRect.rect.size;
            var boundsRect = bounds.rect;
            var desiredCenter = CalculatePosition(anchorRect, menuSize, boundsRect, anchorGap, edgePadding);
            var worldCenter = bounds.TransformPoint(desiredCenter);
            var parent = menuRect.parent as RectTransform;
            var parentPosition = parent != null ? parent.InverseTransformPoint(worldCenter) : worldCenter;

            menuRect.anchorMin = new Vector2(0.5f, 0.5f);
            menuRect.anchorMax = new Vector2(0.5f, 0.5f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.anchoredPosition = new Vector2(parentPosition.x, parentPosition.y);
        }

        public static Vector2 CalculatePosition(
            Rect anchor,
            Vector2 menuSize,
            Rect bounds,
            float gap,
            float padding)
        {
            var safeGap = Mathf.Max(0f, gap);
            var safePadding = Mathf.Max(0f, padding);
            var halfSize = Vector2.Max(Vector2.zero, menuSize) * 0.5f;
            var inner = Rect.MinMaxRect(
                bounds.xMin + safePadding,
                bounds.yMin + safePadding,
                bounds.xMax - safePadding,
                bounds.yMax - safePadding);

            var horizontalCenter = anchor.center.y;
            var verticalCenter = anchor.center.x;
            var right = new Vector2(anchor.xMax + safeGap + halfSize.x, horizontalCenter);
            var left = new Vector2(anchor.xMin - safeGap - halfSize.x, horizontalCenter);
            var below = new Vector2(verticalCenter, anchor.yMin - safeGap - halfSize.y);
            var above = new Vector2(verticalCenter, anchor.yMax + safeGap + halfSize.y);

            Vector2 selected;
            if (Fits(right, halfSize, inner))
            {
                selected = right;
            }
            else if (Fits(left, halfSize, inner))
            {
                selected = left;
            }
            else if (Fits(below, halfSize, inner))
            {
                selected = below;
            }
            else if (Fits(above, halfSize, inner))
            {
                selected = above;
            }
            else
            {
                var rightSpace = inner.xMax - anchor.xMax;
                var leftSpace = anchor.xMin - inner.xMin;
                selected = rightSpace >= leftSpace ? right : left;
            }

            selected.x = ClampCenter(selected.x, inner.xMin, inner.xMax, halfSize.x);
            selected.y = ClampCenter(selected.y, inner.yMin, inner.yMax, halfSize.y);
            return selected;
        }

        private static bool Fits(Vector2 center, Vector2 halfSize, Rect bounds)
        {
            return center.x - halfSize.x >= bounds.xMin &&
                   center.x + halfSize.x <= bounds.xMax &&
                   center.y - halfSize.y >= bounds.yMin &&
                   center.y + halfSize.y <= bounds.yMax;
        }

        private static float ClampCenter(float value, float minimum, float maximum, float halfSize)
        {
            var low = minimum + halfSize;
            var high = maximum - halfSize;
            return low <= high ? Mathf.Clamp(value, low, high) : (minimum + maximum) * 0.5f;
        }
    }
}
