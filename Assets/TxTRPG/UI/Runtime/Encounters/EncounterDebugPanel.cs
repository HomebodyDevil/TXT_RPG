using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Encounters
{
    // The five encounter roll values edited by the debug panel. Chances are 0..1, weights 0..10.
    public readonly struct EncounterDebugValues : IEquatable<EncounterDebugValues>
    {
        public EncounterDebugValues(float occurrenceChance, float inclusionChance, float combatWeight, float eventWeight, float restWeight)
        {
            OccurrenceChance = occurrenceChance;
            InclusionChance = inclusionChance;
            CombatWeight = combatWeight;
            EventWeight = eventWeight;
            RestWeight = restWeight;
        }

        public float OccurrenceChance { get; }
        public float InclusionChance { get; }
        public float CombatWeight { get; }
        public float EventWeight { get; }
        public float RestWeight { get; }

        public bool Equals(EncounterDebugValues other) =>
            OccurrenceChance.Equals(other.OccurrenceChance) && InclusionChance.Equals(other.InclusionChance) &&
            CombatWeight.Equals(other.CombatWeight) && EventWeight.Equals(other.EventWeight) && RestWeight.Equals(other.RestWeight);

        public override bool Equals(object obj) => obj is EncounterDebugValues other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(OccurrenceChance, InclusionChance, CombatWeight, EventWeight, RestWeight);
        public override string ToString() => $"{OccurrenceChance}/{InclusionChance}/{CombatWeight}/{EventWeight}/{RestWeight}";
    }

    // Development-only utility for the encounter roll: sliders for the roll values, a roll button, a reset button and the
    // last result. It knows nothing about the roll rules; the owner listens to the events and calls SetValues/ShowResult.
    // Chance sliders store whole percents (0..100), weight sliders tenths (0..100 = 0.0..10.0).
    [DisallowMultipleComponent]
    public sealed class EncounterDebugPanel : MonoBehaviour
    {
        public const float MaximumWeight = 10f;
        public const string ExpandedLabel = "접기";
        public const string CollapsedLabel = "펼치기";

        [SerializeField] private Slider occurrenceSlider;
        [SerializeField] private TMP_Text occurrenceValue;
        [SerializeField] private Slider inclusionSlider;
        [SerializeField] private TMP_Text inclusionValue;
        [SerializeField] private Slider combatWeightSlider;
        [SerializeField] private TMP_Text combatWeightValue;
        [SerializeField] private Slider eventWeightSlider;
        [SerializeField] private TMP_Text eventWeightValue;
        [SerializeField] private Slider restWeightSlider;
        [SerializeField] private TMP_Text restWeightValue;
        [SerializeField] private Button rollButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button collapseButton;
        [SerializeField] private TMP_Text collapseLabel;
        [SerializeField] private GameObject body;
        [SerializeField] private TMP_Text resultText;

        private bool listening;

        public event Action<EncounterDebugValues> ValuesChanged;
        public event Action RollRequested;
        public event Action ResetRequested;

        public Button RollButton => rollButton;
        public Button ResetButton => resetButton;
        public Button CollapseButton => collapseButton;
        public Slider OccurrenceSlider => occurrenceSlider;
        public Slider InclusionSlider => inclusionSlider;
        public Slider CombatWeightSlider => combatWeightSlider;
        public Slider EventWeightSlider => eventWeightSlider;
        public Slider RestWeightSlider => restWeightSlider;
        public TMP_Text OccurrenceValue => occurrenceValue;
        public TMP_Text InclusionValue => inclusionValue;
        public TMP_Text CombatWeightValue => combatWeightValue;
        public TMP_Text EventWeightValue => eventWeightValue;
        public TMP_Text RestWeightValue => restWeightValue;
        public TMP_Text ResultText => resultText;
        public GameObject Body => body;
        public bool IsExpanded => body == null || body.activeSelf;

        public EncounterDebugValues CurrentValues => new(
            Chance(occurrenceSlider), Chance(inclusionSlider), Weight(combatWeightSlider), Weight(eventWeightSlider), Weight(restWeightSlider));

        public bool IsValid(out string missing)
        {
            missing = occurrenceSlider == null ? nameof(occurrenceSlider) : inclusionSlider == null ? nameof(inclusionSlider)
                : combatWeightSlider == null ? nameof(combatWeightSlider) : eventWeightSlider == null ? nameof(eventWeightSlider)
                : restWeightSlider == null ? nameof(restWeightSlider) : rollButton == null ? nameof(rollButton)
                : resetButton == null ? nameof(resetButton) : collapseButton == null ? nameof(collapseButton)
                : body == null ? nameof(body) : resultText == null ? nameof(resultText) : string.Empty;
            return missing.Length == 0;
        }

        public static string FormatChance(float value) =>
            $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString(CultureInfo.InvariantCulture)}%";

        public static string FormatWeight(float value) =>
            Mathf.Clamp(value, 0f, MaximumWeight).ToString("0.0", CultureInfo.InvariantCulture);

        private void Awake() => Listen();

        private void OnDestroy()
        {
            if (!listening) return;
            foreach (var slider in Sliders()) if (slider != null) slider.onValueChanged.RemoveListener(OnSliderChanged);
            if (rollButton != null) rollButton.onClick.RemoveListener(OnRoll);
            if (resetButton != null) resetButton.onClick.RemoveListener(OnReset);
            if (collapseButton != null) collapseButton.onClick.RemoveListener(ToggleExpanded);
            listening = false;
        }

        // Shows the values without raising ValuesChanged.
        public void SetValues(EncounterDebugValues values)
        {
            Listen();
            SetWithoutNotify(occurrenceSlider, Mathf.Round(Mathf.Clamp01(values.OccurrenceChance) * 100f));
            SetWithoutNotify(inclusionSlider, Mathf.Round(Mathf.Clamp01(values.InclusionChance) * 100f));
            SetWithoutNotify(combatWeightSlider, Mathf.Round(Mathf.Clamp(values.CombatWeight, 0f, MaximumWeight) * 10f));
            SetWithoutNotify(eventWeightSlider, Mathf.Round(Mathf.Clamp(values.EventWeight, 0f, MaximumWeight) * 10f));
            SetWithoutNotify(restWeightSlider, Mathf.Round(Mathf.Clamp(values.RestWeight, 0f, MaximumWeight) * 10f));
            RefreshValueTexts();
        }

        public void ShowResult(string message)
        {
            if (resultText != null) resultText.text = message ?? string.Empty;
        }

        public void SetExpanded(bool expanded)
        {
            Listen();
            if (body != null) body.SetActive(expanded);
            if (collapseLabel != null) collapseLabel.text = expanded ? ExpandedLabel : CollapsedLabel;
        }

        public void ToggleExpanded() => SetExpanded(!IsExpanded);

        private void Listen()
        {
            if (listening) return;
            listening = true;
            foreach (var slider in Sliders()) if (slider != null) slider.onValueChanged.AddListener(OnSliderChanged);
            if (rollButton != null) rollButton.onClick.AddListener(OnRoll);
            if (resetButton != null) resetButton.onClick.AddListener(OnReset);
            if (collapseButton != null) collapseButton.onClick.AddListener(ToggleExpanded);
        }

        private Slider[] Sliders() => new[] { occurrenceSlider, inclusionSlider, combatWeightSlider, eventWeightSlider, restWeightSlider };

        private void OnSliderChanged(float _)
        {
            RefreshValueTexts();
            ValuesChanged?.Invoke(CurrentValues);
        }

        private void OnRoll() => RollRequested?.Invoke();
        private void OnReset() => ResetRequested?.Invoke();

        private void RefreshValueTexts()
        {
            var values = CurrentValues;
            SetText(occurrenceValue, FormatChance(values.OccurrenceChance));
            SetText(inclusionValue, FormatChance(values.InclusionChance));
            SetText(combatWeightValue, FormatWeight(values.CombatWeight));
            SetText(eventWeightValue, FormatWeight(values.EventWeight));
            SetText(restWeightValue, FormatWeight(values.RestWeight));
        }

        private static float Chance(Slider slider) => slider != null ? slider.value / 100f : 0f;
        private static float Weight(Slider slider) => slider != null ? slider.value / 10f : 0f;
        private static void SetWithoutNotify(Slider slider, float value) { if (slider != null) slider.SetValueWithoutNotify(value); }
        private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value; }

#if UNITY_EDITOR
        public void ConfigureSlidersForEditor(Slider occurrence, TMP_Text occurrenceText, Slider inclusion, TMP_Text inclusionText,
            Slider combat, TMP_Text combatText, Slider @event, TMP_Text eventText, Slider rest, TMP_Text restText)
        {
            occurrenceSlider = occurrence; occurrenceValue = occurrenceText; inclusionSlider = inclusion; inclusionValue = inclusionText;
            combatWeightSlider = combat; combatWeightValue = combatText; eventWeightSlider = @event; eventWeightValue = eventText;
            restWeightSlider = rest; restWeightValue = restText;
        }

        public void ConfigureControlsForEditor(Button roll, Button reset, Button collapse, TMP_Text collapseText, GameObject bodyRoot, TMP_Text result)
        { rollButton = roll; resetButton = reset; collapseButton = collapse; collapseLabel = collapseText; body = bodyRoot; resultText = result; }
#endif
    }
}
