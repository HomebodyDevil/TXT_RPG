namespace TxTRPG.Gameplay.Dice
{
    public readonly struct DiceRollResult
    {
        public DiceRollResult(int faceIndex, int value)
        {
            FaceIndex = faceIndex;
            Value = value;
        }

        public int FaceIndex { get; }
        public int Value { get; }
    }
}
