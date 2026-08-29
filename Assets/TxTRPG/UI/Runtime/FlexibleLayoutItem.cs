using UnityEngine;

namespace TxTRPG.UI
{
    public enum FlexibleLayoutSizeMode
    {
        Weighted,
        Fixed
    }

    [DisallowMultipleComponent]
    public sealed class FlexibleLayoutItem : MonoBehaviour
    {
        [SerializeField] private FlexibleLayoutSizeMode sizeMode = FlexibleLayoutSizeMode.Weighted;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(0f)] private float fixedSize = 100f;
        [SerializeField, Min(0f)] private float minimumSize;
        [SerializeField, Min(0f)] private float maximumSize;

        public FlexibleLayoutSizeMode SizeMode => sizeMode;
        public float Weight => Mathf.Max(0f, weight);
        public float FixedSize => Mathf.Max(0f, fixedSize);
        public float MinimumSize => Mathf.Max(0f, minimumSize);
        public float MaximumSize => Mathf.Max(0f, maximumSize);

        public void Configure(
            FlexibleLayoutSizeMode mode,
            float newWeight = 1f,
            float newFixedSize = 100f,
            float newMinimumSize = 0f,
            float newMaximumSize = 0f)
        {
            sizeMode = mode;
            weight = Mathf.Max(0f, newWeight);
            fixedSize = Mathf.Max(0f, newFixedSize);
            minimumSize = Mathf.Max(0f, newMinimumSize);
            maximumSize = Mathf.Max(0f, newMaximumSize);
            MarkParentDirty();
        }

        private void OnEnable()
        {
            MarkParentDirty();
        }

        private void OnDisable()
        {
            MarkParentDirty();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            weight = Mathf.Max(0f, weight);
            fixedSize = Mathf.Max(0f, fixedSize);
            minimumSize = Mathf.Max(0f, minimumSize);
            maximumSize = Mathf.Max(0f, maximumSize);
            MarkParentDirty();
        }
#endif

        private void MarkParentDirty()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null)
            {
                UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(parent);
            }
        }
    }
}
