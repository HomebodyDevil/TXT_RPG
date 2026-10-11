using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Skins available to SlotMachinePanel and the one the developer selected. The selection is stored as the skin ID, so
    // renaming or moving a skin asset keeps it. List order is the display order of a future skin selection UI.
    // Validation failures never stop the game: the panel then falls back to the Prefab's default look.
    [CreateAssetMenu(menuName = "TxT RPG/Slot Machine/Panel Skin Catalog", fileName = "SlotMachinePanelSkinCatalog")]
    public sealed class SlotMachinePanelSkinCatalog : ScriptableObject
    {
        [SerializeField] private List<SlotMachinePanelSkin> skins = new();
        [SerializeField, Tooltip("ID of the skin the panel uses (chosen from the registered skins in the Inspector).")]
        private string selectedSkinId = string.Empty;

        public IReadOnlyList<SlotMachinePanelSkin> Skins => skins;
        public string SelectedSkinId => (selectedSkinId ?? string.Empty).Trim();
        public SlotMachinePanelSkin SelectedSkin => TryGet(SelectedSkinId, out var skin) ? skin : null;

        public bool TryGet(string skinId, out SlotMachinePanelSkin skin)
        {
            skin = null;
            var id = (skinId ?? string.Empty).Trim();
            if (id.Length == 0 || skins == null) return false;
            foreach (var candidate in skins)
            {
                if (candidate == null || candidate.SkinId != id) continue;
                skin = candidate;
                return true;
            }
            return false;
        }

        // Rejects empty entries, empty or duplicate IDs and a selected ID that is not registered.
        public bool Validate(out string reason)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (skins?.Count ?? 0); i++)
            {
                var skin = skins[i];
                if (skin == null) { reason = $"skin entry {i} is empty"; return false; }
                if (skin.SkinId.Length == 0) { reason = $"skin '{skin.name}' has no ID"; return false; }
                if (!ids.Add(skin.SkinId)) { reason = $"skin ID '{skin.SkinId}' is registered more than once"; return false; }
            }
            if (!ids.Contains(SelectedSkinId))
            {
                reason = SelectedSkinId.Length == 0 ? "no skin is selected" : $"the selected skin ID '{SelectedSkinId}' is not registered";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private void OnValidate() => selectedSkinId = SelectedSkinId;

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<SlotMachinePanelSkin> targetSkins, string targetSelectedSkinId)
        {
            skins = new List<SlotMachinePanelSkin>(targetSkins ?? Array.Empty<SlotMachinePanelSkin>());
            selectedSkinId = targetSelectedSkinId ?? string.Empty;
        }
#endif
    }
}
