using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.Exploration
{
    /// <summary>
    /// Placed on an overlay such as ExplorationNodePanel. While the overlay is active, the panels it covers stop
    /// accepting controller and keyboard navigation (their CanvasGroups become non-interactable). Pointer input is
    /// already blocked by the overlay's raycast target. The previous interactable values are restored when the overlay
    /// is hidden, so states owned by the covered panels (for example a disabled results button) are unchanged.
    /// A covered CanvasGroup may also be driven by another component (PanelStartupController enables input after its
    /// reveal, and Unity allows one CanvasGroup per object). If that component turns interactable on while the overlay is
    /// shown, the value is kept as the state to restore and the group is blocked again in the same frame's LateUpdate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoveredPanelInteractionBlocker : MonoBehaviour
    {
        [Tooltip("CanvasGroups of the panels this overlay covers while it is shown.")]
        [SerializeField] private CanvasGroup[] coveredPanels = System.Array.Empty<CanvasGroup>();

        private bool[] previousInteractable = System.Array.Empty<bool>();
        private bool blocking;

        public bool IsBlocking => blocking;

        public void ConfigureForEditor(CanvasGroup[] panels)
        {
            coveredPanels = panels ?? System.Array.Empty<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (coveredPanels == null || coveredPanels.Length == 0)
            {
                return;
            }

            if (previousInteractable.Length != coveredPanels.Length)
            {
                previousInteractable = new bool[coveredPanels.Length];
            }

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (var i = 0; i < coveredPanels.Length; i++)
            {
                var group = coveredPanels[i];
                if (group == null)
                {
                    continue;
                }

                previousInteractable[i] = group.interactable;
                group.interactable = false;
                if (selected != null && selected.transform.IsChildOf(group.transform))
                {
                    // Do not leave focus on a control that is now hidden behind the overlay.
                    EventSystem.current.SetSelectedGameObject(null);
                    selected = null;
                }
            }

            blocking = true;
        }

        private void LateUpdate()
        {
            if (!blocking)
            {
                return;
            }

            for (var i = 0; i < coveredPanels.Length && i < previousInteractable.Length; i++)
            {
                var group = coveredPanels[i];
                if (group != null && group.interactable)
                {
                    previousInteractable[i] = true;
                    group.interactable = false;
                }
            }
        }

        private void OnDisable()
        {
            if (!blocking)
            {
                return;
            }

            for (var i = 0; i < coveredPanels.Length && i < previousInteractable.Length; i++)
            {
                if (coveredPanels[i] != null)
                {
                    coveredPanels[i].interactable = previousInteractable[i];
                }
            }

            blocking = false;
        }
    }
}
