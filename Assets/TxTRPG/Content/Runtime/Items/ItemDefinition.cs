using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Content.Items
{
    public enum ItemEffectKind { Unsupported, Healing }

    [CreateAssetMenu(menuName = "TxT RPG/Content/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string displayNameLocalizationKey = string.Empty;
        [SerializeField] private string iconAssetId = string.Empty;
        [SerializeField] private string categoryId = "misc";
        [SerializeField] private ItemEffectKind effectKind = ItemEffectKind.Unsupported;
        [SerializeField, Min(0)] private int effectAmount;

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public string DisplayNameLocalizationKey => displayNameLocalizationKey?.Trim() ?? string.Empty;
        public string IconAssetId => iconAssetId?.Trim() ?? string.Empty;
        public string CategoryId => string.IsNullOrWhiteSpace(categoryId) ? "misc" : categoryId.Trim();
        public ItemEffectKind EffectKind => effectKind;
        public int EffectAmount => Mathf.Max(0, effectAmount);

#if UNITY_EDITOR
        public void ConfigureForEditor(string id, string nameKey, string iconId, ItemEffectKind kind, int amount)
        {
            definitionId = id?.Trim() ?? string.Empty;
            displayNameLocalizationKey = nameKey?.Trim() ?? string.Empty;
            iconAssetId = iconId?.Trim() ?? string.Empty;
            effectKind = kind;
            effectAmount = Mathf.Max(0, amount);
        }

        public void SetCategoryForEditor(string value) => categoryId = string.IsNullOrWhiteSpace(value) ? "misc" : value.Trim();
#endif
    }
}
