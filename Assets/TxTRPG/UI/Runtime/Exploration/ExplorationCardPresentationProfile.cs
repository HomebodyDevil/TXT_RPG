using System;
using UnityEngine;

namespace TxTRPG.UI.Exploration
{
    [Serializable]
    public struct ExplorationCardTextEffectSettings
    {
        public bool waveEnabled;
        [Range(0f, 4f)] public float waveAmplitude;
        [Min(0f)] public float waveSpeed;
        [Range(0f, 2f)] public float characterPhase;
        public bool gradientEnabled;
        public Color gradientStart;
        public Color gradientEnd;
        [Min(0f)] public float gradientSpeed;
        [Range(-1f, 1f)] public float gradientDirection;
    }

    [CreateAssetMenu(menuName = "TxT RPG/UI/Exploration Card Presentation Profile")]
    public sealed class ExplorationCardPresentationProfile : ScriptableObject
    {
        [Header("Readable Text Colors")]
        public Color titleColor = new(0.953f, 0.965f, 0.98f, 1f);
        public Color descriptionColor = new(0.827f, 0.863f, 0.91f, 1f);
        public Color statusColor = new(1f, 0.945f, 0.812f, 1f);
        [Header("Input Feedback")]
        public Color normalTint = Color.white;
        public Color highlightedTint = new(1.08f, 1.08f, 1.08f, 1f);
        public Color pressedTint = new(0.82f, 0.88f, 1f, 1f);
        public Color confirmingTint = new(0.72f, 0.9f, 1.12f, 1f);
        public Color disabledTint = new(0.62f, 0.65f, 0.72f, 1f);
        [Range(1f, 1.08f)] public float highlightedScale = 1.025f;
        [Range(0.9f, 1f)] public float pressedScale = 0.98f;
        [Min(0f)] public float transitionDuration = 0.1f;
        [Min(0f)] public float confirmationDuration = 0.15f;
        [Header("Hover / Navigation Pulse")]
        public bool pulseEnabled = true;
        [Tooltip("Absolute MotionRoot scale, not multiplied by Highlighted Scale.")]
        [Range(.9f, 1.08f)] public float pulseMinScale = 1f;
        [Range(.9f, 1.08f)] public float pulseMaxScale = 1.03f;
        [Tooltip("Seconds for one complete expansion and contraction. Invalid values use a static scale.")]
        [Min(.1f)] public float pulsePeriod = 1.8f;
        [Header("Text Effects (Off By Default)")]
        public ExplorationCardTextEffectSettings titleEffect;
        public ExplorationCardTextEffectSettings descriptionEffect;
        public ExplorationCardTextEffectSettings statusEffect;

        public float SafeConfirmationDuration => float.IsFinite(confirmationDuration) ? Mathf.Max(0f, confirmationDuration) : 0f;

        public float EvaluatePulse(double elapsed)
        {
            if (!pulseEnabled || !float.IsFinite(pulsePeriod) || pulsePeriod <= 0f) return 1f;
            var first = SafeScale(pulseMinScale);
            var second = SafeScale(pulseMaxScale);
            var phase = double.IsFinite(elapsed) ? Math.Max(0d, elapsed) % pulsePeriod / pulsePeriod : 0d;
            return Mathf.Lerp(Mathf.Min(first, second), Mathf.Max(first, second), (float)((1d - Math.Cos(2d * Math.PI * phase)) * .5d));
        }

        public static float SafeScale(float value) => float.IsFinite(value) && value > 0f ? Mathf.Clamp(value, .9f, 1.08f) : 1f;
    }

    public static class ExplorationCardPlayerPreferences
    {
        private const string EffectsKey = "TxTRPG.UI.ExplorationCardEffects";
        private const string ReduceMotionKey = "TxTRPG.UI.ExplorationReduceMotion";
        public static event Action Changed;
        public static bool EffectsEnabled { get => ReadBool(EffectsKey, true); set => WriteBool(EffectsKey, value); }
        public static bool ReduceMotion { get => ReadBool(ReduceMotionKey, false); set => WriteBool(ReduceMotionKey, value); }
        public static bool AllowsMotion => EffectsEnabled && !ReduceMotion;

        private static bool ReadBool(string key, bool fallback)
        {
            if (!PlayerPrefs.HasKey(key)) return fallback;
            var value = PlayerPrefs.GetInt(key, fallback ? 1 : 0);
            return value is 0 or 1 ? value == 1 : fallback;
        }
        private static void WriteBool(string key, bool value)
        {
            if (ReadBool(key, !value) == value && PlayerPrefs.HasKey(key)) return;
            PlayerPrefs.SetInt(key, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke();
        }
    }
}
