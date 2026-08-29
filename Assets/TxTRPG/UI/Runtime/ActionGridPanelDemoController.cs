using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public sealed class ActionGridPanelDemoController : MonoBehaviour,
        IActionMenuProvider,
        IActionCommandExecutor
    {
        [SerializeField] private ActionGridPanel target;
        [SerializeField] private ActionGridPanelDemoData data;

        private void Start()
        {
            if (target == null || data == null)
            {
                Debug.LogWarning("Action grid demo references are incomplete.", this);
                return;
            }

            target.CloseContextMenu();
            target.SetServices(this, this);
            target.SetEntries(data.CreateEntries());
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
