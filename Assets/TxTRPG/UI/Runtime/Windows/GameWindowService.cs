using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.UI.Windows
{
    public sealed class GameWindowService : MonoBehaviour
    {
        [SerializeField] private ModalWindowHost host;
        [SerializeField] private List<GameWindowPage> pages = new();
        private CancellationTokenSource requestCancellation;
        private IGameWindowPage currentPage;
        private GameObject focusReturnTarget;
        private ModalOpenRequest lastRequest;
        public bool IsOpen => currentPage != null || (host != null && host.IsVisible);
        public string CurrentPageId => currentPage?.PageId ?? string.Empty;
        public bool BlocksGameplayInput => IsOpen;
        public ModalWindowHost Host => host;
        public IReadOnlyList<GameWindowPage> Pages => pages;
        public ModalOpenRequest LastRequest => lastRequest;
        public bool HasPage(string pageId) => host != null && pages.Exists(candidate => candidate != null && string.Equals(candidate.PageId, pageId?.Trim(), StringComparison.Ordinal));
        private void Awake() { if (host != null) { host.CloseRequested += Close; host.RetryRequested += Retry; host.SetVisible(false); } foreach (var page in pages) page?.Hide(); }
        private void OnDestroy() { if (host != null) { host.CloseRequested -= Close; host.RetryRequested -= Retry; } requestCancellation?.Cancel(); foreach (var page in pages) page?.DisposePage(); }
        public ModalOpenRequest CreateRequest(string pageId, GameObject returnFocus = null)
        {
            var page = pages.Find(candidate => candidate != null && string.Equals(candidate.PageId, pageId?.Trim(), StringComparison.Ordinal));
            return page?.CreateDefaultRequest(returnFocus);
        }
        public Task<bool> OpenAsync(string pageId, GameObject returnFocus = null)
        {
            var request = CreateRequest(pageId, returnFocus);
            if (request != null) return OpenAsync(request);
            Debug.LogError($"Unknown game-window page '{pageId}'.", this);
            return Task.FromResult(false);
        }
        public async Task<bool> OpenAsync(ModalOpenRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.WindowId) || !Enum.IsDefined(typeof(ModalContentKind), request.ContentKind))
            { Debug.LogError("Invalid modal open request.", this); return false; }
            var page = pages.Find(candidate => candidate != null && string.Equals(candidate.PageId, request.WindowId, StringComparison.Ordinal));
            if (page == null) { Debug.LogError($"Unknown game-window page '{request.WindowId}'.", this); return false; }
            lastRequest = request;
            requestCancellation?.Cancel(); requestCancellation?.Dispose(); requestCancellation = new CancellationTokenSource();
            var token = requestCancellation.Token; if (!IsOpen) focusReturnTarget = request.ReturnFocus;
            currentPage?.Hide(); currentPage = null; host.SetVisible(true); host.SetLoading(true); host.SetError(string.Empty);
            host.SetTitle(string.IsNullOrEmpty(request.Title) ? page.DisplayTitle : request.Title);
            try { page.ApplyRequest(request); await page.PrepareAsync(token); token.ThrowIfCancellationRequested(); currentPage = page; page.Show(); host.SetLoading(false); if (EventSystem.current != null && page.InitialFocus != null) EventSystem.current.SetSelectedGameObject(page.InitialFocus); return true; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    host.SetLoading(false);
                    var displayException = exception as IGameWindowDisplayException;
                    host.SetError(displayException?.UserMessage ?? "창을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
                    Debug.LogError($"Game window '{request.WindowId}' failed. Code: {displayException?.ErrorCode ?? "window.prepare-failed"}. {exception}", page as UnityEngine.Object);
                }
                return false;
            }
        }
        private async void Retry()
        {
            if (lastRequest == null) return;
            await OpenAsync(lastRequest);
        }
        public void Close()
        { requestCancellation?.Cancel(); requestCancellation?.Dispose(); requestCancellation = null; currentPage?.Hide(); currentPage = null; host.SetVisible(false); if (EventSystem.current != null && focusReturnTarget != null && focusReturnTarget.activeInHierarchy) EventSystem.current.SetSelectedGameObject(focusReturnTarget); focusReturnTarget = null; lastRequest = null; }
#if UNITY_EDITOR
        public void ConfigureForEditor(ModalWindowHost targetHost, IEnumerable<GameWindowPage> configuredPages) { host = targetHost; pages = new List<GameWindowPage>(configuredPages); }
#endif
    }
}
