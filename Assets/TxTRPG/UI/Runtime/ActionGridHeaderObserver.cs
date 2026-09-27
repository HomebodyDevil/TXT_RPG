using UnityEngine;
namespace TxTRPG.UI
{
    [ExecuteAlways]
    public sealed class ActionGridHeaderObserver : MonoBehaviour
    {
        [SerializeField] private ActionGridSurfaceLayout surface;
        public void Bind(ActionGridSurfaceLayout value) => surface = value;
        private void OnEnable() => surface?.HeaderStateChanged();
        private void OnDisable() => surface?.HeaderStateChanged();
    }
}
