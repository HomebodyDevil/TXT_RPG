using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Windows
{
    public static class GamePageIds
    {
        public const string System = "system";
        public const string Inventory = "inventory";
        public const string Status = "status";
    }

    public interface IGameWindowPage
    {
        string PageId { get; }
        Task PrepareAsync(CancellationToken cancellationToken);
        void Show();
        void Hide();
        void DisposePage();
        GameObject InitialFocus { get; }
    }

    public abstract class GameWindowPage : MonoBehaviour, IGameWindowPage
    {
        [SerializeField] private string pageId = string.Empty;
        [SerializeField] private GameObject initialFocus;
        public string PageId => pageId?.Trim() ?? string.Empty;
        public GameObject InitialFocus => initialFocus;
        public virtual Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual void Show() => gameObject.SetActive(true);
        public virtual void Hide() => gameObject.SetActive(false);
        public virtual void DisposePage() { }
#if UNITY_EDITOR
        public void ConfigureForEditor(string id, GameObject focus = null) { pageId = id; initialFocus = focus; }
#endif
    }

    public sealed class MessageGameWindowPage : GameWindowPage
    {
        [SerializeField] private TMP_Text message;
        [SerializeField, TextArea] private string configuredMessage = string.Empty;
        public void SetMessage(string value) { configuredMessage = value ?? string.Empty; if (message != null) message.text = configuredMessage; }
        public override Task PrepareAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); if (message != null) message.text = configuredMessage; return Task.CompletedTask; }
#if UNITY_EDITOR
        public void ConfigureMessageForEditor(TMP_Text target, string value) { message = target; configuredMessage = value; }
#endif
    }

    public sealed class ModalWindowHost : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private GameObject loadingState;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private Button closeButton;
        public event Action CloseRequested;
        public bool IsVisible => canvasGroup != null && canvasGroup.interactable;
        private void Awake() { if (closeButton != null) closeButton.onClick.AddListener(RequestClose); }
        private void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose); }
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(true);
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
        public void SetLoading(bool loading) { if (loadingState != null) loadingState.SetActive(loading); }
        public void SetError(string value) { if (errorText != null) { errorText.text = value ?? string.Empty; errorText.gameObject.SetActive(!string.IsNullOrEmpty(value)); } }
        public void SetTitle(string value) { if (title != null) title.text = value ?? string.Empty; }
        public void RequestClose() => CloseRequested?.Invoke();
#if UNITY_EDITOR
        public void ConfigureForEditor(CanvasGroup group, TMP_Text titleText, GameObject loading, TMP_Text error, Button close)
        { canvasGroup = group; title = titleText; loadingState = loading; errorText = error; closeButton = close; }
#endif
    }

    public sealed class GameWindowService : MonoBehaviour
    {
        [SerializeField] private ModalWindowHost host;
        [SerializeField] private List<GameWindowPage> pages = new();
        private CancellationTokenSource requestCancellation;
        private IGameWindowPage currentPage;
        private GameObject focusReturnTarget;
        public bool IsOpen => currentPage != null || (host != null && host.IsVisible);
        public bool BlocksGameplayInput => IsOpen;
        public ModalWindowHost Host => host;
        public IReadOnlyList<GameWindowPage> Pages => pages;
        public bool HasPage(string pageId) => host != null && pages.Exists(candidate => candidate != null &&
            string.Equals(candidate.PageId, pageId?.Trim(), StringComparison.Ordinal));

        private void Awake() { if (host != null) { host.CloseRequested += Close; host.SetVisible(false); } foreach (var page in pages) page?.Hide(); }
        private void OnDestroy() { if (host != null) host.CloseRequested -= Close; requestCancellation?.Cancel(); foreach (var page in pages) page?.DisposePage(); }
        public async Task<bool> OpenAsync(string pageId, GameObject returnFocus = null)
        {
            var page = pages.Find(candidate => candidate != null && string.Equals(candidate.PageId, pageId?.Trim(), StringComparison.Ordinal));
            if (page == null) { Debug.LogError($"Unknown game-window page '{pageId}'.", this); return false; }
            requestCancellation?.Cancel(); requestCancellation?.Dispose();
            requestCancellation = new CancellationTokenSource();
            var token = requestCancellation.Token;
            if (!IsOpen) focusReturnTarget = returnFocus;
            currentPage?.Hide(); currentPage = null;
            host.SetVisible(true); host.SetLoading(true); host.SetError(string.Empty); host.SetTitle(page.PageId);
            try
            {
                await page.PrepareAsync(token);
                token.ThrowIfCancellationRequested();
                currentPage = page; page.Show(); host.SetLoading(false);
                if (EventSystem.current != null && page.InitialFocus != null) EventSystem.current.SetSelectedGameObject(page.InitialFocus);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
            catch (Exception exception) { if (!token.IsCancellationRequested) { host.SetLoading(false); host.SetError(exception.Message); } return false; }
        }
        public void Close()
        {
            requestCancellation?.Cancel(); requestCancellation?.Dispose(); requestCancellation = null;
            currentPage?.Hide(); currentPage = null; host.SetVisible(false);
            if (EventSystem.current != null && focusReturnTarget != null && focusReturnTarget.activeInHierarchy) EventSystem.current.SetSelectedGameObject(focusReturnTarget);
            focusReturnTarget = null;
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(ModalWindowHost targetHost, IEnumerable<GameWindowPage> configuredPages) { host = targetHost; pages = new List<GameWindowPage>(configuredPages); }
#endif
    }
}
