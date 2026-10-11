using UnityEngine;

namespace TxTRPG.UI.SlotMachine
{
    // Image set of SlotMachinePanel (slot-machine-and-runes.md, "SlotMachinePanel 스킨"). Every entry is optional: an
    // empty entry shows the Prefab's recorded default. The panel only reads a skin; it never changes at runtime.
    // skinId is stable: it is stored by a future player choice, so never change a shipped ID.
    [CreateAssetMenu(menuName = "TxT RPG/Slot Machine/Panel Skin", fileName = "SlotMachinePanelSkin")]
    public sealed class SlotMachinePanelSkin : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable ID, for example slot-skin.default. Never change it after shipping.")]
        private string skinId = string.Empty;
        [SerializeField, Tooltip("Name for a future skin selection UI.")]
        private string displayName = string.Empty;
        [SerializeField, Tooltip("Optional preview for a future skin selection UI.")]
        private Sprite previewSprite;

        [Header("Payline")]
        [SerializeField, Tooltip("With a sprite the payline is this image; without one the code-drawn border is shown (a color override recolors it).")]
        private SlotSkinImage paylineFrame = new();
        [SerializeField, Tooltip("How far the payline image extends outward from the payline cell range, in canvas units.")]
        private SlotSkinOffset paylineOutset = new();

        [Header("Panel images")]
        [SerializeField] private SlotSkinImage panelBackground = new();
        [SerializeField] private SlotSkinImage reelMachineFrame = new();
        [SerializeField, Tooltip("Applied to both LeftEdge and RightEdge.")]
        private SlotSkinImage reelMachineEdge = new();
        [SerializeField, Tooltip("Each reel's Frame (cylinder shading).")]
        private SlotSkinImage reelBackground = new();
        [SerializeField, Tooltip("Each reel's Window/ShadeOverlay.")]
        private SlotSkinImage reelShadeOverlay = new();
        [SerializeField] private SlotSkinImage reelDivider = new();
        [SerializeField] private SlotSkinImage summaryBackground = new();
        [SerializeField] private SlotSkinImage totalBoxBackground = new();
        [SerializeField] private SlotSkinImage runeTableBackground = new();
        [SerializeField] private SlotSkinImage runeRowBackground = new();

        [Header("Text colors (keep enough contrast with the backgrounds)")]
        [SerializeField, Tooltip("Reel digits and afterimages; the per-cell and afterimage alpha multiply this alpha.")]
        private SlotSkinColor reelTextColor = new();
        [SerializeField, Tooltip("Total label and value.")]
        private SlotSkinColor darkSurfaceTextColor = new();
        [SerializeField, Tooltip("Column titles and rune table rows.")]
        private SlotSkinColor lightSurfaceTextColor = new();

        public string SkinId => (skinId ?? string.Empty).Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SkinId : displayName;
        public Sprite PreviewSprite => previewSprite;
        public SlotSkinImage PaylineFrame => paylineFrame;
        public SlotSkinOffset PaylineOutset => paylineOutset;
        public SlotSkinImage PanelBackground => panelBackground;
        public SlotSkinImage ReelMachineFrame => reelMachineFrame;
        public SlotSkinImage ReelMachineEdge => reelMachineEdge;
        public SlotSkinImage ReelBackground => reelBackground;
        public SlotSkinImage ReelShadeOverlay => reelShadeOverlay;
        public SlotSkinImage ReelDivider => reelDivider;
        public SlotSkinImage SummaryBackground => summaryBackground;
        public SlotSkinImage TotalBoxBackground => totalBoxBackground;
        public SlotSkinImage RuneTableBackground => runeTableBackground;
        public SlotSkinImage RuneRowBackground => runeRowBackground;
        public SlotSkinColor ReelTextColor => reelTextColor;
        public SlotSkinColor DarkSurfaceTextColor => darkSurfaceTextColor;
        public SlotSkinColor LightSurfaceTextColor => lightSurfaceTextColor;

        // True when the payline is drawn from an image instead of the code-drawn border.
        public bool HasPaylineImage => paylineFrame != null && paylineFrame.Sprite != null;

        // Only trims the ID and clamps this asset's own values; never touches other assets or Scenes.
        private void OnValidate()
        {
            skinId = SkinId;
            foreach (var entry in new[] { paylineFrame, panelBackground, reelMachineFrame, reelMachineEdge, reelBackground, reelShadeOverlay,
                         reelDivider, summaryBackground, totalBoxBackground, runeTableBackground, runeRowBackground })
                entry?.Validate();
        }
    }
}
