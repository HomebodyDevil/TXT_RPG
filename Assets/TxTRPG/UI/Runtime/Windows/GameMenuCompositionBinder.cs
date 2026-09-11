using UnityEngine;

namespace TxTRPG.UI.Windows
{
    [DisallowMultipleComponent]
    public sealed class GameMenuCompositionBinder : MonoBehaviour
    {
        [SerializeField] private GameMenuPanel menu;
        [SerializeField] private GameWindowService windowService;
        [SerializeField] private ActionGridPanel quickItemGrid;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private bool replaceInspectorService;
        private bool ownsBinding;
        public GameMenuPanel Menu => menu;
        public GameWindowService WindowService => windowService;
        public RectTransform OverlayRoot => overlayRoot;
        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();
        public bool Bind()
        {
            if (menu == null || windowService == null) return false;
            ownsBinding = menu.BindExternalDependencies(windowService, quickItemGrid, replaceInspectorService);
            return ownsBinding;
        }
        public void Unbind()
        {
            if (ownsBinding && menu != null) menu.UnbindExternalDependencies(windowService);
            ownsBinding = false;
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(GameMenuPanel targetMenu, GameWindowService service,
            ActionGridPanel grid, RectTransform overlay, bool replaceExisting = false)
        { menu = targetMenu; windowService = service; quickItemGrid = grid; overlayRoot = overlay; replaceInspectorService = replaceExisting; }
#endif
    }
}
