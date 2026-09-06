using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    public readonly struct CharacterNamePresentation
    {
        public CharacterNamePresentation(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
        }

        public string DisplayName { get; }
    }

    public readonly struct HealthPresentation
    {
        public HealthPresentation(
            int current,
            int maximum,
            string label,
            string valueText)
        {
            Maximum = Mathf.Max(1, maximum);
            Current = Mathf.Clamp(current, 0, Maximum);
            Label = label ?? string.Empty;
            ValueText = valueText ?? string.Empty;
        }

        public int Current { get; }
        public int Maximum { get; }
        public float Ratio => Mathf.Clamp01(Current / (float)Maximum);
        public string Label { get; }
        public string ValueText { get; }
    }

    public readonly struct CharacterStatusPresentation
    {
        public CharacterStatusPresentation(
            in CharacterNamePresentation name,
            in HealthPresentation health)
        {
            Name = name;
            Health = health;
        }

        public CharacterNamePresentation Name { get; }
        public HealthPresentation Health { get; }
    }

    public abstract class CharacterStatusElement : MonoBehaviour
    {
        public abstract void Apply(in CharacterStatusPresentation presentation);
        public abstract void Clear();
    }

    [DisallowMultipleComponent]
    public sealed class CharacterStatusPanel : MonoBehaviour
    {
        [SerializeField] private bool autoCollectChildElements = true;
        [SerializeField] private List<CharacterStatusElement> elements = new();

        private CharacterStatusPresentation currentPresentation;
        private bool hasPresentation;

        public IReadOnlyList<CharacterStatusElement> Elements => elements;

        public void Apply(in CharacterStatusPresentation presentation)
        {
            currentPresentation = presentation;
            hasPresentation = true;
            for (var index = 0; index < elements.Count; index++)
            {
                elements[index]?.Apply(presentation);
            }
        }

        public void Clear()
        {
            hasPresentation = false;
            currentPresentation = default;
            for (var index = 0; index < elements.Count; index++)
            {
                elements[index]?.Clear();
            }
        }

        public void Register(CharacterStatusElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (elements.Contains(element))
            {
                return;
            }

            elements.Add(element);
            if (hasPresentation)
            {
                element.Apply(currentPresentation);
            }
        }

        public bool Unregister(CharacterStatusElement element)
        {
            return element != null && elements.Remove(element);
        }

        public void RefreshElements()
        {
            elements.Clear();
            elements.AddRange(GetComponentsInChildren<CharacterStatusElement>(true));
        }

        private void Awake()
        {
            if (autoCollectChildElements)
            {
                RefreshElements();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            elements ??= new List<CharacterStatusElement>();
            if (autoCollectChildElements)
            {
                RefreshElements();
            }
        }
#endif
    }
}
