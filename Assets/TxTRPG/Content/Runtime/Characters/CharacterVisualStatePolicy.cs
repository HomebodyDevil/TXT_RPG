using System;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Content.Characters
{
    [CreateAssetMenu(
        fileName = "CharacterVisualStatePolicy",
        menuName = "TxT RPG/Characters/Visual State Policy")]
    public sealed class CharacterVisualStatePolicy : ScriptableObject,
        ICharacterVisualStateResolver
    {
        public const string NormalStateId = "normal";
        public const string InjuredStateId = "injured";
        public const string CriticalStateId = "critical";
        public const string DefeatedStateId = "defeated";

        [SerializeField, Range(0f, 1f)] private float injuredHealthRatio = 0.5f;
        [SerializeField, Range(0f, 1f)] private float criticalHealthRatio = 0.2f;

        public float InjuredHealthRatio => injuredHealthRatio;
        public float CriticalHealthRatio => criticalHealthRatio;

        public string Resolve(CharacterRuntimeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return Resolve(state.Health.Current, state.Health.Maximum);
        }

        public string Resolve(int currentHealth, int maximumHealth)
        {
            if (maximumHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            var clampedHealth = Mathf.Clamp(currentHealth, 0, maximumHealth);
            if (clampedHealth == 0)
            {
                return DefeatedStateId;
            }

            var ratio = clampedHealth / (float)maximumHealth;
            if (ratio <= criticalHealthRatio)
            {
                return CriticalStateId;
            }
            if (ratio <= injuredHealthRatio)
            {
                return InjuredStateId;
            }
            return NormalStateId;
        }

        public bool TryValidate(out string error)
        {
            if (criticalHealthRatio < 0f || criticalHealthRatio >= injuredHealthRatio ||
                injuredHealthRatio > 1f)
            {
                error = "Visual state health ratios must satisfy " +
                        "0 <= critical < injured <= 1.";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(float criticalRatio, float injuredRatio)
        {
            criticalHealthRatio = criticalRatio;
            injuredHealthRatio = injuredRatio;
        }
#endif

        private void OnValidate()
        {
            criticalHealthRatio = Mathf.Clamp01(criticalHealthRatio);
            injuredHealthRatio = Mathf.Clamp01(injuredHealthRatio);
        }
    }
}
