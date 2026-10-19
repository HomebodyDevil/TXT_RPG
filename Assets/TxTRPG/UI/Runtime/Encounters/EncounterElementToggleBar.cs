using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Encounters
{
    // Horizontal row of multi-select element toggles. It only displays a selection owned elsewhere: SetSelected never
    // notifies, user changes are reported through SelectionChanged. Views are pooled; extra views are deactivated.
    // Left/right navigation is explicit between active buttons and does not wrap.
    [DisallowMultipleComponent]
    public sealed class EncounterElementToggleBar : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private EncounterElementToggleView togglePrefab;

        private readonly List<EncounterElementToggleView> views = new();
        private int activeCount;

        public event Action<string, bool> SelectionChanged;

        public ScrollRect ScrollRect => scrollRect;
        public RectTransform Content => content;
        public EncounterElementToggleView TogglePrefab => togglePrefab;
        public IReadOnlyList<EncounterElementToggleView> Views => views;
        public int ActiveCount => activeCount;

        public bool IsValid(out string missing)
        {
            missing = scrollRect == null ? nameof(scrollRect) : content == null ? nameof(content) : togglePrefab == null ? nameof(togglePrefab) : string.Empty;
            return missing.Length == 0;
        }

        public void SetEntries(IReadOnlyList<EncounterElementToggleEntry> entries)
        {
            if (!IsValid(out var missing))
            {
                Debug.LogError($"EncounterElementToggleBar is missing {missing}.", this);
                return;
            }
            activeCount = entries?.Count ?? 0;
            for (var index = 0; index < activeCount; index++)
            {
                var view = index < views.Count ? views[index] : Create();
                view.gameObject.SetActive(true);
                view.Bind(entries[index]);
            }
            for (var index = activeCount; index < views.Count; index++)
            {
                views[index].SetIsOnWithoutNotify(false);
                views[index].gameObject.SetActive(false);
            }
            LinkNavigation();
        }

        public void SetSelected(string id, bool value)
        {
            var view = Find(id);
            if (view != null) view.SetIsOnWithoutNotify(value);
        }

        public bool IsSelected(string id)
        {
            var view = Find(id);
            return view != null && view.IsOn;
        }

        public EncounterElementToggleView Find(string id)
        {
            var key = id?.Trim() ?? string.Empty;
            for (var index = 0; index < activeCount; index++)
                if (string.Equals(views[index].Id, key, StringComparison.Ordinal)) return views[index];
            return null;
        }

        private EncounterElementToggleView Create()
        {
            var view = Instantiate(togglePrefab, content, false);
            view.name = $"{togglePrefab.name} {views.Count + 1}";
            view.ValueChanged += OnViewChanged;
            views.Add(view);
            return view;
        }

        private void OnViewChanged(string id, bool value) => SelectionChanged?.Invoke(id, value);

        private void LinkNavigation()
        {
            for (var index = 0; index < activeCount; index++)
            {
                var toggle = views[index].Toggle;
                if (toggle == null) continue;
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = index > 0 ? views[index - 1].Toggle : null,
                    selectOnRight = index + 1 < activeCount ? views[index + 1].Toggle : null
                };
                toggle.navigation = navigation;
            }
        }

        private void OnDestroy()
        {
            foreach (var view in views)
                if (view != null) view.ValueChanged -= OnViewChanged;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(ScrollRect scroll, RectTransform contentRoot, EncounterElementToggleView prefab)
        { scrollRect = scroll; content = contentRoot; togglePrefab = prefab; }
#endif
    }
}
