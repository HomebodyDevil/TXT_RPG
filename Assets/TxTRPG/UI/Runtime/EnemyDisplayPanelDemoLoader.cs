using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class EnemyDisplayPanelDemoLoader : PanelInitialDataLoader
    {
        [SerializeField] private EnemyDisplayPanel target;
        [SerializeField] private Enemy2DDisplayBackend backend;
        [SerializeField] private EnemyDisplayPanelDemoData data;

        public override bool HasInitialData => target != null && backend != null && data != null;

        public override async Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken)
        {
            if (!HasInitialData)
            {
                Debug.LogWarning("Enemy display demo references are incomplete.", this);
                return new PanelLoadResult(false, errorCode: "missing-enemy-demo-references");
            }

            backend.SetAppearanceDefinitions(new[] { data.AppearanceDefinition });
            target.SetEnemies(data.CreatePresentations());
            await target.WhenAssetsReady;
            if (target.BackgroundRenderer != null)
            {
                await target.BackgroundRenderer.WhenAssetsReady;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return PanelLoadResult.Success;
        }
    }
}
