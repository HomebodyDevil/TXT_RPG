using System.Collections.Generic;

namespace TxTRPG.UI
{
    public interface ICharacterAppearanceDefinitionReceiver
    {
        void SetAppearanceDefinitions(
            IEnumerable<CharacterAppearanceDefinition> definitions);
    }
}
