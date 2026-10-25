using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    // Authored tooltip body: an ordered list of blocks. It does not know what it describes (rune, item, skill), so several
    // targets may share one content asset; catalogs such as RuneTooltipCatalog pair target IDs with contents.
    [CreateAssetMenu(menuName = "TxT RPG/UI/Tooltip Content", fileName = "TooltipContent")]
    public sealed class TooltipContent : ScriptableObject
    {
        [SerializeField] private List<TooltipBlock> blocks = new();

        public IReadOnlyList<TooltipBlock> Blocks => blocks ?? (IReadOnlyList<TooltipBlock>)Array.Empty<TooltipBlock>();

        public void SetBlocks(IEnumerable<TooltipBlock> source) =>
            blocks = source != null ? new List<TooltipBlock>(source) : new List<TooltipBlock>();
    }
}
