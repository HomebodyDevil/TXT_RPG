using TxTRPG.Gameplay.Dice;

namespace TxTRPG.Application.Dice
{
    public interface IDiceEffectDisplayNameProvider
    {
        string GetDisplayName(DiceEffectKind effectKind);
    }

    public sealed class KoreanDiceEffectDisplayNameProvider : IDiceEffectDisplayNameProvider
    {
        public static readonly KoreanDiceEffectDisplayNameProvider Instance = new KoreanDiceEffectDisplayNameProvider();
        private KoreanDiceEffectDisplayNameProvider() { }

        public string GetDisplayName(DiceEffectKind effectKind)
        {
            switch (effectKind)
            {
                case DiceEffectKind.Attack: return "공격";
                case DiceEffectKind.Heal: return "회복";
                default: return "알 수 없는 효과";
            }
        }
    }

    public static class DiceRollStoryFormatter
    {
        public static string Format(PlayerDieRoll roll, IDiceEffectDisplayNameProvider names = null)
        {
            names = names ?? KoreanDiceEffectDisplayNameProvider.Instance;
            return $"[주사위] {roll.Order}번 주사위 ({roll.DisplayName}): {names.GetDisplayName(roll.Result.EffectKind)} {roll.Result.Amount}";
        }
    }
}
