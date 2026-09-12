using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.Windows
{
    public abstract class GameWindowPage : MonoBehaviour, IGameWindowPage
    {
        [SerializeField] private string pageId = string.Empty;
        [SerializeField] private string displayTitle = string.Empty;
        [SerializeField] private GameObject initialFocus;
        public string PageId => pageId?.Trim() ?? string.Empty;
        public string DisplayTitle => string.IsNullOrWhiteSpace(displayTitle) ? PageId : displayTitle.Trim();
        public GameObject InitialFocus => initialFocus;
        public virtual ModalOpenRequest CreateDefaultRequest(GameObject returnFocus = null) =>
            new(PageId, ModalContentKind.CustomContent, DisplayTitle, returnFocus: returnFocus);
        public virtual void ApplyRequest(ModalOpenRequest request)
        {
            if (request == null || !string.Equals(request.WindowId, PageId, System.StringComparison.Ordinal))
                throw new ModalRequestException("window.request-page-mismatch", $"Page '{PageId}' received a request for '{request?.WindowId ?? "<null>"}'.");
            if (request.ContentKind != ModalContentKind.CustomContent)
                throw new ModalRequestException("window.request-kind-mismatch", $"Page '{PageId}' does not support content kind '{request.ContentKind}'.");
        }
        public virtual Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
        public virtual void DisposePage() { }
#if UNITY_EDITOR
        public void ConfigureForEditor(string id, GameObject focus = null, string title = null)
        { pageId = id; initialFocus = focus; displayTitle = title ?? string.Empty; }
#endif
    }
}
