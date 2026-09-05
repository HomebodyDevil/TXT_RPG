using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class Enemy2DDisplayBackend : EnemyDisplayBackendBase
    {
        [SerializeField] private Enemy2DView viewPrefab;
        [SerializeField] private RectTransform viewHost;
        [SerializeField] private RectTransform poolRoot;
        [SerializeField] private EnemyLayoutStrategyBase layoutStrategy;
        [SerializeField] private List<EnemyAppearanceDefinition> appearanceDefinitions = new();
        [SerializeField, Min(1)] private int maximumVisibleEnemies = 8;
        [SerializeField] private bool bringTargetedEnemyToFront = true;

        private readonly Dictionary<string, Enemy2DView> activeViews =
            new(StringComparer.Ordinal);
        private readonly List<Enemy2DView> activeOrder = new();
        private readonly Stack<Enemy2DView> pooledViews = new();
        private readonly List<EnemyPlacement> placements = new();
        private IAssetProvider assetProvider;

        public override int ActiveViewCount => activeViews.Count;
        public int PooledViewCount => pooledViews.Count;
        public override Task WhenAssetsReady => Task.WhenAll(
            activeOrder.Select(view => view.WhenAssetsReady));

        private void Awake()
        {
            if (viewHost == null)
            {
                return;
            }

            foreach (var view in viewHost.GetComponentsInChildren<Enemy2DView>(true))
            {
                if (view == null || view == viewPrefab)
                {
                    continue;
                }

                view.Unbind();
                view.transform.SetParent(poolRoot != null ? poolRoot : transform, false);
                pooledViews.Push(view);
            }
        }

        public void SetAppearanceDefinitions(IEnumerable<EnemyAppearanceDefinition> definitions)
        {
            appearanceDefinitions.Clear();
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null)
                    {
                        appearanceDefinitions.Add(definition);
                    }
                }
            }

            foreach (var view in activeOrder)
            {
                view.SetAppearanceDefinitions(appearanceDefinitions);
            }
        }

        public override void SetEnemies(IReadOnlyList<EnemyPresentation> enemies)
        {
            enemies ??= Array.Empty<EnemyPresentation>();
            var maximum = Mathf.Max(1, maximumVisibleEnemies);
            var incoming = new List<EnemyPresentation>(Mathf.Min(enemies.Count, maximum));
            var desired = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < enemies.Count && incoming.Count < maximum; i++)
            {
                var enemy = enemies[i];
                if (enemy.IsValid && desired.Add(enemy.InstanceId))
                {
                    incoming.Add(enemy);
                }
            }

            if (enemies.Count > incoming.Count && incoming.Count == maximum)
            {
                Debug.LogWarning(
                    $"EnemyDisplayPanel received {enemies.Count} enemies, but this backend can display " +
                    $"only {maximum}. Increase Maximum Visible Enemies if this is intentional.",
                    this);
            }

            for (var i = activeOrder.Count - 1; i >= 0; i--)
            {
                var view = activeOrder[i];
                if (!desired.Contains(view.InstanceId))
                {
                    Release(view);
                }
            }

            activeOrder.Clear();
            for (var i = 0; i < incoming.Count; i++)
            {
                var enemy = incoming[i];
                if (!activeViews.TryGetValue(enemy.InstanceId, out var view))
                {
                    view = Acquire();
                    if (view == null)
                    {
                        continue;
                    }
                    activeViews.Add(enemy.InstanceId, view);
                    view.Bind(enemy);
                }
                else
                {
                    view.UpdatePresentation(enemy);
                }
                activeOrder.Add(view);
            }

            ApplyFormation();
            ApplyTargetSiblingOrder();
        }

        public override void UpdateEnemy(in EnemyPresentation enemy)
        {
            if (activeViews.TryGetValue(enemy.InstanceId, out var view))
            {
                view.UpdatePresentation(enemy);
                ApplyTargetSiblingOrder();
            }
        }

        public override void RemoveEnemy(string instanceId)
        {
            if (activeViews.TryGetValue(instanceId ?? string.Empty, out var view))
            {
                Release(view);
                ApplyFormation();
            }
        }

        public override void PlayAnimation(string instanceId, string animationId)
        {
            if (activeViews.TryGetValue(instanceId ?? string.Empty, out var view))
            {
                view.PlayAnimation(animationId);
            }
        }

        public override void PlayEffect(string instanceId, string effectId)
        {
            if (activeViews.TryGetValue(instanceId ?? string.Empty, out var view))
            {
                view.PlayEffect(effectId);
            }
        }

        public override void SetTargeted(string instanceId, bool targeted)
        {
            if (activeViews.TryGetValue(instanceId ?? string.Empty, out var view))
            {
                view.SetTargeted(targeted);
                ApplyTargetSiblingOrder();
            }
        }

        public override void SetAssetProvider(IAssetProvider provider)
        {
            assetProvider = provider;
            foreach (var view in activeOrder)
            {
                view.SetAssetProvider(provider);
            }
            foreach (var view in pooledViews)
            {
                view.SetAssetProvider(provider);
            }
        }

        public bool TryGetView(string instanceId, out Enemy2DView view) =>
            activeViews.TryGetValue(instanceId ?? string.Empty, out view);

        public override void Clear()
        {
            for (var i = activeOrder.Count - 1; i >= 0; i--)
            {
                Release(activeOrder[i]);
            }
            activeOrder.Clear();
        }

        private Enemy2DView Acquire()
        {
            if (viewPrefab == null || viewHost == null)
            {
                Debug.LogError("Enemy2DDisplayBackend requires both View Prefab and View Host.", this);
                return null;
            }

            var view = pooledViews.Count > 0
                ? pooledViews.Pop()
                : Instantiate(viewPrefab, viewHost);
            view.transform.SetParent(viewHost, false);
            view.SetAppearanceDefinitions(appearanceDefinitions);
            view.SetAssetProvider(assetProvider);
            view.gameObject.SetActive(true);
            return view;
        }

        private void Release(Enemy2DView view)
        {
            if (view == null)
            {
                return;
            }

            activeViews.Remove(view.InstanceId);
            activeOrder.Remove(view);
            view.Unbind();
            view.transform.SetParent(poolRoot != null ? poolRoot : transform, false);
            pooledViews.Push(view);
        }

        private void ApplyFormation()
        {
            if (layoutStrategy == null || viewHost == null)
            {
                return;
            }

            var rect = viewHost.rect;
            layoutStrategy.CalculatePlacements(activeOrder.Count, rect, placements);
            for (var i = 0; i < activeOrder.Count && i < placements.Count; i++)
            {
                var viewRect = (RectTransform)activeOrder[i].transform;
                var placement = placements[i];
                viewRect.anchorMin = new Vector2(0.5f, 0.5f);
                viewRect.anchorMax = new Vector2(0.5f, 0.5f);
                viewRect.pivot = new Vector2(0.5f, 0.5f);
                viewRect.anchoredPosition = placement.AnchoredPosition;
                viewRect.localScale = Vector3.one * placement.Scale;
                viewRect.SetSiblingIndex(Mathf.Min(placement.SiblingIndex, viewHost.childCount - 1));
            }
        }

        private void ApplyTargetSiblingOrder()
        {
            if (!bringTargetedEnemyToFront)
            {
                return;
            }

            foreach (var view in activeOrder)
            {
                if (view.IsTargeted)
                {
                    view.transform.SetAsLastSibling();
                }
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                ApplyFormation();
                ApplyTargetSiblingOrder();
            }
        }

        private void OnDestroy() => Clear();
    }
}
