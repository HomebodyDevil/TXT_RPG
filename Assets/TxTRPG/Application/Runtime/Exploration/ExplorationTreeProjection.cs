using System.Linq;
using System.Collections.Generic;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.UI.Exploration;
namespace TxTRPG.Application.Exploration
{
    public static class ExplorationTreeProjection
    {
        public static ExplorationTreeSnapshot Create(ExplorationRunState run)
        {
            var path=new HashSet<string>(run.SelectedPath,System.StringComparer.Ordinal);
            return new ExplorationTreeSnapshot(run.RunId,run.Nodes.Select(n=>new ExplorationTreeNode(n.Id,n.ParentId,n.Depth,n.SiblingIndex,n.TypeId,n.Status,path.Contains(n.Id))));
        }
    }
}
