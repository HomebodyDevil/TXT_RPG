using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class CharacterEffectPlayer : MonoBehaviour
    {
        [SerializeField] private Animator effectAnimator;

        public void Play(string effectId)
        {
            if (effectAnimator == null || string.IsNullOrWhiteSpace(effectId))
            {
                return;
            }

            effectAnimator.Play(effectId, 0, 0f);
        }

        public void Clear()
        {
            if (effectAnimator != null)
            {
                effectAnimator.Rebind();
                effectAnimator.Update(0f);
            }
        }
    }
}
