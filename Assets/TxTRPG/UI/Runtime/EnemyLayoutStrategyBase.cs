using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI
{
    public abstract class EnemyLayoutStrategyBase : MonoBehaviour
    {
        public abstract void CalculatePlacements(
            int enemyCount,
            Rect availableArea,
            IList<EnemyPlacement> results);
    }
}
