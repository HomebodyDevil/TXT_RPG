using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Runes;
using UnityEngine;

namespace TxTRPG.Application.Runes
{
    // Development-only rune definitions, merged into the session rune catalog (PlayerSessionHost). They are never owned at
    // the start: the DEBUG panel rune tab adds and removes them. Every ID starts with "rune.test." and every effect is
    // Test, so they receive no allocation and never change a game result. Adding a test rune kind means adding an entry
    // here and, optionally, an icon (TemporaryRuneIconCatalog) and tooltip content (TemporaryRuneTooltipCatalog).
    [CreateAssetMenu(menuName = "TxT RPG/Temporary Test Rune Configuration", fileName = "TemporaryTestRuneConfiguration")]
    public sealed class TemporaryTestRuneConfiguration : ScriptableObject
    {
        public const string IdPrefix = "rune.test.";
        public const string TooltipRuneId = "rune.test.tooltip";
        public const string LongRuneId = "rune.test.long";
        public const string MissingRuneId = "rune.test.missing";

        [SerializeField] private List<TemporaryRuneDefinitionEntry> definitions = new()
        {
            new TemporaryRuneDefinitionEntry(TooltipRuneId, "테스트 룬", RuneEffectKind.Test),
            new TemporaryRuneDefinitionEntry(LongRuneId, "긴 테스트 룬", RuneEffectKind.Test),
            new TemporaryRuneDefinitionEntry(MissingRuneId, "내용 없는 테스트 룬", RuneEffectKind.Test)
        };

        public IReadOnlyList<TemporaryRuneDefinitionEntry> Definitions => definitions ?? (IReadOnlyList<TemporaryRuneDefinitionEntry>)Array.Empty<TemporaryRuneDefinitionEntry>();

        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var source = Definitions;
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (entry == null || entry.Id.Length == 0) { reason = $"Test rune entry {i + 1} has an empty id."; return false; }
                if (!ids.Add(entry.Id)) { reason = $"Test rune id '{entry.Id}' is duplicated."; return false; }
                if (!entry.Id.StartsWith(IdPrefix, StringComparison.Ordinal)) { reason = $"Test rune id '{entry.Id}' does not start with '{IdPrefix}'."; return false; }
                if (entry.EffectKind != RuneEffectKind.Test) { reason = $"Test rune '{entry.Id}' has effect '{entry.EffectKind}'; only Test is allowed."; return false; }
            }
            reason = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<TemporaryRuneDefinitionEntry> runeDefinitions) =>
            definitions = new List<TemporaryRuneDefinitionEntry>(runeDefinitions ?? Array.Empty<TemporaryRuneDefinitionEntry>());
#endif
    }
}
