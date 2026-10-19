namespace TxTRPG.UI.Runes
{
    // One rune line as the rune grid shows it. Key identifies the line (card reuse), DefinitionId looks up the icon,
    // DisplayName is the fallback card text and StackCount is the badge number (the line weight).
    public readonly struct RuneGridEntryDisplay
    {
        public RuneGridEntryDisplay(string key, string definitionId, string displayName, int stackCount)
        {
            Key = key ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            StackCount = stackCount < 0 ? 0 : stackCount;
        }

        public string Key { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public int StackCount { get; }
    }
}
