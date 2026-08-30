using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class ActionGridPanelDemoController : PanelInitialDataLoader,
        IActionMenuProvider,
        IActionCommandExecutor
    {
        [SerializeField] private ActionGridPanel target;
        [SerializeField] private ActionGridPanelDemoData data;

        public override bool HasInitialData => target != null && data != null;

        public override async Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken)
        {
            if (target == null || data == null)
            {
                Debug.LogWarning("Action grid demo references are incomplete.", this);
                return new PanelLoadResult(false, errorCode: "missing-action-grid-demo-references");
            }

            target.CloseContextMenu();
            target.SetServices(this, this);
            target.SetEntries(data.CreateEntries());
            await target.WhenAssetsReady;
            cancellationToken.ThrowIfCancellationRequested();
            return PanelLoadResult.Success;
        }

        public IReadOnlyList<ActionMenuOption> GetOptions(string entryId)
        {
            if (entryId.StartsWith("skill-", System.StringComparison.Ordinal))
            {
                return new[]
                {
                    new ActionMenuOption("use", "Use"),
                    new ActionMenuOption("assign", "Assign Shortcut"),
                    new ActionMenuOption("inspect", "Inspect")
                };
            }

            return new[]
            {
                new ActionMenuOption("use", "Use"),
                new ActionMenuOption("equip", "Equip", !entryId.Contains("potion"), "Not equipment"),
                new ActionMenuOption("inspect", "Inspect"),
                new ActionMenuOption("discard", "Discard")
            };
        }

        public Task<ActionCommandResult> ExecuteAsync(
            string entryId,
            string commandId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Log($"Demo action requested: {entryId}/{commandId}", this);
            return Task.FromResult(new ActionCommandResult(true, "Demo command completed."));
        }
    }
}
