namespace TxTRPG.Gameplay.Dice
{
    public readonly struct DiceRollResult
    {
        public DiceRollResult(int faceIndex, DiceFace face)
        {
            FaceIndex = faceIndex;
            EffectKind = face.EffectKind;
            Amount = face.Amount;
        }

        public int FaceIndex { get; }
        public DiceEffectKind EffectKind { get; }
        public int Amount { get; }
    }
}
