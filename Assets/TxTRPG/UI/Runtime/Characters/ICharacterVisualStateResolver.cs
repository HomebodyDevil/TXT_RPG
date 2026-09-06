using TxTRPG.Gameplay.Characters;

namespace TxTRPG.UI
{
    public interface ICharacterVisualStateResolver
    {
        string Resolve(CharacterRuntimeState state);
    }
}
