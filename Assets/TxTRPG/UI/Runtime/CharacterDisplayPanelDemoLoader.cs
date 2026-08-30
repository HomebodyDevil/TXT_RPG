using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class CharacterDisplayPanelDemoLoader : PanelInitialDataLoader
    {
        [SerializeField] private CharacterDisplayPanel target;
        [SerializeField] private Character2DView characterView;
        [SerializeField] private CharacterDisplayPanelDemoData data;

        public override bool HasInitialData => target != null && characterView != null && data != null;

        public override async Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken)
        {
            if (target == null || characterView == null || data == null)
            {
                Debug.LogWarning("Character display demo references are incomplete.", this);
                return new PanelLoadResult(false, errorCode: "missing-character-demo-references");
            }

            characterView.SetAppearanceDefinitions(new[] { data.AppearanceDefinition });
            target.ShowCharacterImmediately(data.ToPresentation());
            await characterView.WhenAssetsReady;
            if (target.BackgroundRenderer != null) await target.BackgroundRenderer.WhenAssetsReady;
            cancellationToken.ThrowIfCancellationRequested();
            return PanelLoadResult.Success;
        }
    }
}
