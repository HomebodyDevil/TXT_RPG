using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public abstract class EnemyDisplayBackendBase : MonoBehaviour
    {
        public abstract int ActiveViewCount { get; }
        public abstract Task WhenAssetsReady { get; }
        public abstract void SetEnemies(IReadOnlyList<EnemyPresentation> enemies);
        public abstract void UpdateEnemy(in EnemyPresentation enemy);
        public abstract void RemoveEnemy(string instanceId);
        public abstract void PlayAnimation(string instanceId, string animationId);
        public abstract void PlayEffect(string instanceId, string effectId);
        public abstract void SetTargeted(string instanceId, bool targeted);
        public abstract void SetAssetProvider(IAssetProvider provider);
        public abstract void Clear();
    }
}
