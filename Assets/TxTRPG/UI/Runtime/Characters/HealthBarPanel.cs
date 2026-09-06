using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public abstract class HealthBarEffect : MonoBehaviour
    {
        public abstract void Apply(
            in HealthPresentation previous,
            in HealthPresentation current,
            bool isInitialValue);

        public abstract void Clear();
    }

    [DisallowMultipleComponent]
    public sealed class HealthBarPanel : CharacterStatusElement
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private List<HealthBarEffect> effects = new();

        private HealthPresentation currentPresentation;
        private bool hasPresentation;

        public Slider Slider => slider;

        public override void Apply(in CharacterStatusPresentation presentation)
        {
            var next = presentation.Health;
            ConfigureSlider();
            if (slider != null)
            {
                slider.maxValue = next.Maximum;
                slider.SetValueWithoutNotify(next.Current);
            }

            ApplyOptionalText(labelText, next.Label);
            ApplyOptionalText(valueText, next.ValueText);

            var previous = currentPresentation;
            var isInitialValue = !hasPresentation;
            currentPresentation = next;
            hasPresentation = true;
            for (var index = 0; index < effects.Count; index++)
            {
                effects[index]?.Apply(previous, next, isInitialValue);
            }
        }

        public override void Clear()
        {
            hasPresentation = false;
            currentPresentation = default;
            ConfigureSlider();
            if (slider != null)
            {
                slider.maxValue = 1f;
                slider.SetValueWithoutNotify(0f);
            }
            ClearOptionalText(labelText);
            ClearOptionalText(valueText);
            for (var index = 0; index < effects.Count; index++)
            {
                effects[index]?.Clear();
            }
        }

        public void Configure(
            Slider configuredSlider,
            TMP_Text configuredLabelText = null,
            TMP_Text configuredValueText = null)
        {
            slider = configuredSlider;
            labelText = configuredLabelText;
            valueText = configuredValueText;
            ConfigureSlider();
        }

        public void RegisterEffect(HealthBarEffect effect)
        {
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }
            if (!effects.Contains(effect))
            {
                effects.Add(effect);
                if (hasPresentation)
                {
                    effect.Apply(default, currentPresentation, true);
                }
            }
        }

        public bool UnregisterEffect(HealthBarEffect effect)
        {
            return effect != null && effects.Remove(effect);
        }

        private void Awake()
        {
            ConfigureSlider();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            effects ??= new List<HealthBarEffect>();
            ConfigureSlider();
        }
#endif

        private void ConfigureSlider()
        {
            if (slider == null)
            {
                return;
            }

            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0f;
            slider.wholeNumbers = true;
        }

        private static void ApplyOptionalText(TMP_Text target, string value)
        {
            if (target == null)
            {
                return;
            }

            target.text = value ?? string.Empty;
            target.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }

        private static void ClearOptionalText(TMP_Text target)
        {
            if (target == null)
            {
                return;
            }

            target.text = string.Empty;
            target.gameObject.SetActive(false);
        }
    }
}
