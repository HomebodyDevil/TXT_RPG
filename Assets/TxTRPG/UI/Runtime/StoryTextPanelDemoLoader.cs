using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class StoryTextPanelDemoLoader : PanelInitialDataLoader
    {
        [SerializeField] private StoryTextPanel target;
        [SerializeField] private StoryTextPanelDemoData data;
        [SerializeField] private bool populateOnStart = true;
        [SerializeField] private bool clearBeforePopulate = true;

        public override bool HasInitialData => populateOnStart && target != null && data != null;

        public override async Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PopulateInternal();
            await target.WhenBackgroundReady;
            cancellationToken.ThrowIfCancellationRequested();
            return PanelLoadResult.Success;
        }

        [ContextMenu("Populate Demo Messages")]
        public void Populate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("StoryTextPanel demo messages can be populated in Play Mode.", this);
                return;
            }

            if (target == null || data == null)
            {
                Debug.LogError("StoryTextPanel demo loader requires both a target and demo data.", this);
                return;
            }

            PopulateInternal();
        }

        private void PopulateInternal()
        {
            if (target == null || data == null) return;

            if (clearBeforePopulate)
            {
                target.Clear();
            }

            foreach (var entry in data.Entries)
            {
                var message = entry.ToMessage();
                target.AddMessage(message);
            }
        }
    }
}
