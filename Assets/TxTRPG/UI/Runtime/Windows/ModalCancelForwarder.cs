using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.Windows
{
    // Put on a selectable inside a modal (for example the close button used as the page's initial focus). The EventSystem
    // sends Cancel only to the selected object, so this forwards it to the owning ModalWindowHost.
    [DisallowMultipleComponent]
    public sealed class ModalCancelForwarder : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private ModalWindowHost host;

        public ModalWindowHost Host => host;

        public void OnCancel(BaseEventData eventData)
        {
            if (host != null) host.OnCancel(eventData);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(ModalWindowHost target) => host = target;
#endif
    }
}
