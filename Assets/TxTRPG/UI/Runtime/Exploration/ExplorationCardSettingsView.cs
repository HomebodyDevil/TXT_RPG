using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationCardSettingsView : MonoBehaviour
    {
        [SerializeField] private Toggle effectsToggle;
        [SerializeField] private Toggle reduceMotionToggle;
        private void OnEnable()
        {
            effectsToggle?.SetIsOnWithoutNotify(ExplorationCardPlayerPreferences.EffectsEnabled);
            reduceMotionToggle?.SetIsOnWithoutNotify(ExplorationCardPlayerPreferences.ReduceMotion);
            effectsToggle?.onValueChanged.AddListener(SetEffects); reduceMotionToggle?.onValueChanged.AddListener(SetReduceMotion);
        }
        private void OnDisable() { effectsToggle?.onValueChanged.RemoveListener(SetEffects); reduceMotionToggle?.onValueChanged.RemoveListener(SetReduceMotion); }
        private static void SetEffects(bool value) => ExplorationCardPlayerPreferences.EffectsEnabled = value;
        private static void SetReduceMotion(bool value) => ExplorationCardPlayerPreferences.ReduceMotion = value;
#if UNITY_EDITOR
        public void ConfigureForEditor(Toggle effects, Toggle reduceMotion) { effectsToggle = effects; reduceMotionToggle = reduceMotion; }
#endif
    }
}
