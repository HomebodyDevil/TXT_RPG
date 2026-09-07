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

        protected virtual void OnEnable()
        {
            GetComponentInParent<HealthBarPanel>()?.NotifyEffectEnabled(this);
        }

        protected virtual void OnDisable()
        {
            var panel = GetComponentInParent<HealthBarPanel>();
            if (panel != null) panel.NotifyEffectDisabled(this);
            else Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class HealthBarPanel : CharacterStatusElement
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private RectTransform backgroundVisualRoot;
        [SerializeField] private RectTransform barVisualRoot;
        [SerializeField] private RectTransform fillVisualRoot;
        [SerializeField] private RectTransform borderVisualRoot;
        [SerializeField] private RectTransform textVisualRoot;
        [SerializeField] private List<HealthBarEffect> effects = new();
        [SerializeField] private bool effectsEnabled = true;

        private HealthPresentation currentPresentation;
        private bool hasPresentation;
        private readonly HashSet<HealthBarEffect> appliedEffects = new();

        public Slider Slider => slider;
        public RectTransform BackgroundVisualRoot => backgroundVisualRoot;
        public RectTransform BarVisualRoot => barVisualRoot;
        public RectTransform FillVisualRoot => fillVisualRoot;
        public RectTransform BorderVisualRoot => borderVisualRoot;
        public RectTransform TextVisualRoot => textVisualRoot;
        public bool EffectsEnabled => effectsEnabled;

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
            SynchronizeEffects(previous, next, isInitialValue);
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
            ClearAllEffects();
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

        public void ConfigureVisualRoots(
            RectTransform configuredBackgroundVisualRoot,
            RectTransform configuredBarVisualRoot,
            RectTransform configuredFillVisualRoot,
            RectTransform configuredBorderVisualRoot,
            RectTransform configuredTextVisualRoot)
        {
            backgroundVisualRoot = configuredBackgroundVisualRoot;
            barVisualRoot = configuredBarVisualRoot;
            fillVisualRoot = configuredFillVisualRoot;
            borderVisualRoot = configuredBorderVisualRoot;
            textVisualRoot = configuredTextVisualRoot;
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
            }
            NotifyEffectEnabled(effect);
        }

        public bool UnregisterEffect(HealthBarEffect effect)
        {
            if (effect == null || !effects.Remove(effect)) return false;
            appliedEffects.Remove(effect);
            effect.Clear();
            return true;
        }

        public void SetEffectsEnabled(bool value)
        {
            if (effectsEnabled == value) return;
            effectsEnabled = value;
            if (!value)
            {
                ClearAppliedEffects();
                return;
            }
            if (hasPresentation) SynchronizeEffects(default, currentPresentation, true);
        }

        private void Awake()
        {
            ConfigureSlider();
        }

        private void OnEnable()
        {
            ConfigureSlider();
            if (hasPresentation && effectsEnabled)
                SynchronizeEffects(default, currentPresentation, true);
        }

        private void OnDisable() => ClearAppliedEffects();
        private void OnDestroy() => ClearAppliedEffects();

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

        internal void NotifyEffectEnabled(HealthBarEffect effect)
        {
            if (effect == null || effects == null || !effects.Contains(effect) ||
                !effectsEnabled || !hasPresentation || !effect.isActiveAndEnabled)
            {
                return;
            }
            effect.Apply(default, currentPresentation, true);
            appliedEffects.Add(effect);
        }

        internal void NotifyEffectDisabled(HealthBarEffect effect)
        {
            if (effect != null && appliedEffects.Remove(effect)) effect.Clear();
        }

        private void SynchronizeEffects(
            in HealthPresentation previous,
            in HealthPresentation current,
            bool isInitialValue)
        {
            effects ??= new List<HealthBarEffect>();
            if (!effectsEnabled)
            {
                ClearAppliedEffects();
                return;
            }

            for (var index = 0; index < effects.Count; index++)
            {
                var effect = effects[index];
                if (effect == null) continue;
                if (!effect.isActiveAndEnabled)
                {
                    NotifyEffectDisabled(effect);
                    continue;
                }
                if (isInitialValue && appliedEffects.Contains(effect)) continue;
                effect.Apply(previous, current, isInitialValue);
                appliedEffects.Add(effect);
            }
        }

        private void ClearAppliedEffects()
        {
            if (appliedEffects.Count == 0) return;
            var snapshot = new List<HealthBarEffect>(appliedEffects);
            appliedEffects.Clear();
            for (var index = 0; index < snapshot.Count; index++) snapshot[index]?.Clear();
        }

        private void ClearAllEffects()
        {
            appliedEffects.Clear();
            effects ??= new List<HealthBarEffect>();
            for (var index = 0; index < effects.Count; index++) effects[index]?.Clear();
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
