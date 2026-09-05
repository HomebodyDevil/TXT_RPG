using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class EnemyDisplayPanel : MonoBehaviour
    {
        [SerializeField] private EnemyDisplayBackendBase activeBackend;
        [SerializeField] private PanelBackgroundRenderer backgroundRenderer;
        [SerializeField] private bool singleTarget = true;

        private readonly List<EnemyPresentation> enemies = new();
        private IAssetProvider assetProvider;

        public int EnemyCount => enemies.Count;
        public EnemyDisplayBackendBase ActiveBackend => activeBackend;
        public PanelBackgroundRenderer BackgroundRenderer => backgroundRenderer;
        public Task WhenAssetsReady => activeBackend?.WhenAssetsReady ?? Task.CompletedTask;

        public event Action<IReadOnlyList<EnemyPresentation>> EnemiesChanged;

        public void SetEnemies(IReadOnlyList<EnemyPresentation> presentations)
        {
            enemies.Clear();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (presentations != null)
            {
                for (var i = 0; i < presentations.Count; i++)
                {
                    var enemy = presentations[i];
                    if (!enemy.IsValid || !seen.Add(enemy.InstanceId))
                    {
                        Debug.LogWarning(
                            $"Enemy presentation at index {i} has an invalid or duplicate InstanceId.",
                            this);
                        continue;
                    }
                    enemies.Add(enemy);
                }
            }

            enemies.Sort((left, right) =>
            {
                var formation = left.FormationIndex.CompareTo(right.FormationIndex);
                return formation != 0
                    ? formation
                    : string.CompareOrdinal(left.InstanceId, right.InstanceId);
            });
            NormalizeSingleTarget();
            activeBackend?.SetEnemies(enemies);
            RaiseChanged();
        }

        public bool AddEnemy(in EnemyPresentation enemy)
        {
            if (!enemy.IsValid || FindIndex(enemy.InstanceId) >= 0)
            {
                return false;
            }

            enemies.Add(enemy);
            SetEnemies(enemies.ToArray());
            return true;
        }

        public bool UpdateEnemy(in EnemyPresentation enemy)
        {
            var index = FindIndex(enemy.InstanceId);
            if (index < 0 || !enemy.IsValid)
            {
                return false;
            }

            enemies[index] = enemy;
            SetEnemies(enemies.ToArray());
            return true;
        }

        public bool RemoveEnemy(string enemyInstanceId)
        {
            var index = FindIndex(enemyInstanceId);
            if (index < 0)
            {
                return false;
            }

            enemies.RemoveAt(index);
            activeBackend?.RemoveEnemy(enemyInstanceId);
            RaiseChanged();
            return true;
        }

        public void PlayAnimation(string enemyInstanceId, string animationId) =>
            activeBackend?.PlayAnimation(enemyInstanceId, animationId);

        public void PlayEffect(string enemyInstanceId, string effectId) =>
            activeBackend?.PlayEffect(enemyInstanceId, effectId);

        public bool SetTargeted(string enemyInstanceId, bool targeted)
        {
            var index = FindIndex(enemyInstanceId);
            if (index < 0)
            {
                return false;
            }

            if (singleTarget && targeted)
            {
                for (var i = 0; i < enemies.Count; i++)
                {
                    if (enemies[i].IsTargeted)
                    {
                        enemies[i] = enemies[i].WithTargeted(false);
                        activeBackend?.SetTargeted(enemies[i].InstanceId, false);
                    }
                }
            }

            enemies[index] = enemies[index].WithTargeted(targeted);
            activeBackend?.SetTargeted(enemyInstanceId, targeted);
            RaiseChanged();
            return true;
        }

        public void Clear()
        {
            enemies.Clear();
            activeBackend?.Clear();
            RaiseChanged();
        }

        public void SetBackend(EnemyDisplayBackendBase backend, bool clearPrevious = true)
        {
            if (activeBackend == backend)
            {
                return;
            }

            if (clearPrevious)
            {
                activeBackend?.Clear();
            }
            activeBackend = backend;
            activeBackend?.SetAssetProvider(assetProvider);
            activeBackend?.SetEnemies(enemies);
        }

        public void SetAssetProvider(IAssetProvider provider)
        {
            assetProvider = provider;
            activeBackend?.SetAssetProvider(provider);
        }
        public void ApplyBackground(PanelBackgroundStyle style) => backgroundRenderer?.ApplyStyle(style);
        public void ChangeBackground(PanelBackgroundStyle style, float duration = -1f) =>
            backgroundRenderer?.Change(style, duration);
        public void ClearBackground() => backgroundRenderer?.Clear();
        public void SetBackgroundAssetProvider(IAssetProvider provider) =>
            backgroundRenderer?.SetAssetProvider(provider);

        private int FindIndex(string instanceId) => enemies.FindIndex(enemy =>
            string.Equals(enemy.InstanceId, instanceId ?? string.Empty, StringComparison.Ordinal));

        private void NormalizeSingleTarget()
        {
            if (!singleTarget)
            {
                return;
            }

            var found = false;
            for (var i = 0; i < enemies.Count; i++)
            {
                if (!enemies[i].IsTargeted)
                {
                    continue;
                }
                if (!found)
                {
                    found = true;
                }
                else
                {
                    enemies[i] = enemies[i].WithTargeted(false);
                }
            }
        }

        private void RaiseChanged() => EnemiesChanged?.Invoke(enemies);
    }
}
