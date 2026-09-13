namespace TxTRPG.Gameplay.Dice
{
    public readonly struct DiceFace
    {
        public DiceFace(DiceEffectKind effectKind, int amount)
        {
            EffectKind = effectKind;
            Amount = amount;
        }

        public DiceEffectKind EffectKind { get; }
        public int Amount { get; }
    }
}
