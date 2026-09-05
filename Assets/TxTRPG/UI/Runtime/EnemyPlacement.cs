using UnityEngine;

namespace TxTRPG.UI
{
    public readonly struct EnemyPlacement
    {
        public EnemyPlacement(Vector2 anchoredPosition, float scale, int siblingIndex)
        {
            AnchoredPosition = anchoredPosition;
            Scale = Mathf.Max(0f, scale);
            SiblingIndex = Mathf.Max(0, siblingIndex);
        }

        public Vector2 AnchoredPosition { get; }
        public float Scale { get; }
        public int SiblingIndex { get; }
    }
}
