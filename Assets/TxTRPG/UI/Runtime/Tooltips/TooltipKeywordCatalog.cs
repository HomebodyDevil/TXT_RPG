using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Tooltips
{
    // The keywords that {kw:id} may refer to. IDs are trimmed and compared case-sensitively; with duplicates only the
    // first keyword counts (Validate reports them).
    [CreateAssetMenu(menuName = "TxT RPG/UI/Tooltip Keyword Catalog", fileName = "TooltipKeywordCatalog")]
    public sealed class TooltipKeywordCatalog : ScriptableObject
    {
        [SerializeField] private List<TooltipKeywordDefinition> keywords = new();

        public IReadOnlyList<TooltipKeywordDefinition> Keywords => keywords ?? (IReadOnlyList<TooltipKeywordDefinition>)Array.Empty<TooltipKeywordDefinition>();

        public void SetKeywords(IEnumerable<TooltipKeywordDefinition> source) =>
            keywords = source != null ? new List<TooltipKeywordDefinition>(source) : new List<TooltipKeywordDefinition>();

        public bool TryGet(string keywordId, out TooltipKeywordDefinition keyword)
        {
            keyword = null;
            var id = keywordId?.Trim();
            if (string.IsNullOrEmpty(id)) return false;
            foreach (var candidate in Keywords)
            {
                if (candidate == null || !string.Equals(candidate.Id, id, StringComparison.Ordinal)) continue;
                keyword = candidate;
                return true;
            }
            return false;
        }

        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var source = Keywords;
            for (var i = 0; i < source.Count; i++)
            {
                var keyword = source[i];
                if (keyword == null) { reason = $"Keyword entry {i + 1} is empty."; return false; }
                if (keyword.Id.Length == 0) { reason = $"Keyword '{keyword.name}' has an empty id."; return false; }
                if (!ids.Add(keyword.Id)) { reason = $"Keyword id '{keyword.Id}' is duplicated."; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }
}
