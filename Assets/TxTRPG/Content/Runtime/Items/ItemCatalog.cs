using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Content.Items
{
    [CreateAssetMenu(menuName = "TxT RPG/Content/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> definitions = new();
        public IReadOnlyList<ItemDefinition> Definitions => definitions;
        public bool TryGet(string definitionId, out ItemDefinition definition)
        {
            var id = definitionId?.Trim() ?? string.Empty;
            definition = definitions.Find(candidate => candidate != null && string.Equals(candidate.DefinitionId, id, StringComparison.Ordinal));
            return definition != null;
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<ItemDefinition> values) => definitions = values == null ? new List<ItemDefinition>() : new List<ItemDefinition>(values);
#endif
    }
}
