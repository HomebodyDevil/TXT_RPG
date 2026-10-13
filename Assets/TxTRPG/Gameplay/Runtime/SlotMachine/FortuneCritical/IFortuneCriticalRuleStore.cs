namespace TxTRPG.Gameplay.SlotMachine
{
    // Session Fortune Critical rules that other systems (exploration events, later runes or items) read and change. Every
    // change is validated and keeps the previous rules on failure.
    public interface IFortuneCriticalRuleStore
    {
        FortuneCriticalRules Rules { get; }
        bool TrySetDefaultReelChance(double chance, out string reason);
        // A reel without its own rule gets a copy of the default rule with this chance.
        bool TrySetReelChance(string reelId, double chance, out string reason);
        bool TrySetTotalChance(double chance, out string reason);
        bool TrySetMaximumEffectiveMultiplier(int value, out string reason);
    }
}
