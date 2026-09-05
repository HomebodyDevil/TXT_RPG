using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class ResponsiveHorizontalEnemyLayoutStrategy : EnemyLayoutStrategyBase
    {
        [SerializeField, Min(1)] private int maximumPerRow = 3;
        [SerializeField] private Vector2 referenceViewSize = new(180f, 260f);
        [SerializeField, Min(0f)] private float horizontalSpacing = 24f;
        [SerializeField, Min(0f)] private float verticalSpacing = 12f;
        [SerializeField, Min(0.05f)] private float minimumScale = 0.45f;
        [SerializeField, Min(0.05f)] private float maximumScale = 1f;
        [SerializeField] private Vector2 formationOffset;

        public Vector2 ReferenceViewSize => referenceViewSize;

        public override void CalculatePlacements(
            int enemyCount,
            Rect availableArea,
            IList<EnemyPlacement> results)
        {
            Calculate(
                enemyCount,
                availableArea,
                maximumPerRow,
                referenceViewSize,
                horizontalSpacing,
                verticalSpacing,
                minimumScale,
                maximumScale,
                formationOffset,
                results);
        }

        public static void Calculate(
            int enemyCount,
            Rect availableArea,
            int maximumPerRow,
            Vector2 referenceViewSize,
            float horizontalSpacing,
            float verticalSpacing,
            float minimumScale,
            float maximumScale,
            Vector2 formationOffset,
            IList<EnemyPlacement> results)
        {
            results.Clear();
            enemyCount = Mathf.Max(0, enemyCount);
            if (enemyCount == 0)
            {
                return;
            }

            maximumPerRow = Mathf.Max(1, maximumPerRow);
            referenceViewSize.x = Mathf.Max(1f, referenceViewSize.x);
            referenceViewSize.y = Mathf.Max(1f, referenceViewSize.y);
            horizontalSpacing = Mathf.Max(0f, horizontalSpacing);
            verticalSpacing = Mathf.Max(0f, verticalSpacing);
            minimumScale = Mathf.Max(0.05f, minimumScale);
            maximumScale = Mathf.Max(minimumScale, maximumScale);

            var columns = Mathf.Min(maximumPerRow, enemyCount);
            var rows = Mathf.CeilToInt(enemyCount / (float)columns);
            var widthAtUnitScale = columns * referenceViewSize.x +
                                   Mathf.Max(0, columns - 1) * horizontalSpacing;
            var heightAtUnitScale = rows * referenceViewSize.y +
                                    Mathf.Max(0, rows - 1) * verticalSpacing;
            var widthScale = Mathf.Max(1f, availableArea.width) / widthAtUnitScale;
            var heightScale = Mathf.Max(1f, availableArea.height) / heightAtUnitScale;
            var scale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), minimumScale, maximumScale);
            var stepX = referenceViewSize.x * scale + horizontalSpacing * scale;
            var stepY = referenceViewSize.y * scale + verticalSpacing * scale;

            var placed = 0;
            for (var row = 0; row < rows; row++)
            {
                var rowCount = Mathf.Min(columns, enemyCount - placed);
                var rowWidth = Mathf.Max(0, rowCount - 1) * stepX;
                var y = (rows - 1) * stepY * 0.5f - row * stepY;
                for (var column = 0; column < rowCount; column++)
                {
                    var x = -rowWidth * 0.5f + column * stepX;
                    results.Add(new EnemyPlacement(
                        availableArea.center + formationOffset + new Vector2(x, y),
                        scale,
                        placed));
                    placed++;
                }
            }
        }
    }
}
