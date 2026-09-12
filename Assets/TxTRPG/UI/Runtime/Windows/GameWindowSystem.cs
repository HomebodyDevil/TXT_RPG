using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI.Windows
{
    public enum ModalContentKind { CustomContent = 0, ItemGrid = 1 }

    public interface IModalContentConfiguration
    {
        IModalContentConfiguration CloneForRequest();
    }

    public interface IModalDataProvider
    {
        string ProviderKey { get; }
        bool IsAvailable { get; }
    }

    public sealed class ModalOpenRequest
    {
        public ModalOpenRequest(string windowId, ModalContentKind contentKind, string title,
            IModalContentConfiguration configuration = null, IModalDataProvider dataProvider = null,
            string initialCategoryId = null, int initialPage = 1, GameObject returnFocus = null)
        {
            WindowId = windowId?.Trim() ?? string.Empty;
            ContentKind = contentKind;
            Title = title?.Trim() ?? string.Empty;
            Configuration = configuration?.CloneForRequest();
            DataProvider = dataProvider;
            InitialCategoryId = initialCategoryId?.Trim() ?? string.Empty;
            InitialPage = Math.Max(1, initialPage);
            ReturnFocus = returnFocus;
        }

        public string WindowId { get; }
        public ModalContentKind ContentKind { get; }
        public string Title { get; }
        public IModalContentConfiguration Configuration { get; }
        public IModalDataProvider DataProvider { get; }
        public string InitialCategoryId { get; }
        public int InitialPage { get; }
        public GameObject ReturnFocus { get; }

        public ModalOpenRequest WithPresentation(ModalContentKind contentKind, string title) =>
            new(WindowId, contentKind, string.IsNullOrWhiteSpace(title) ? Title : title,
                Configuration, DataProvider, InitialCategoryId, InitialPage, ReturnFocus);
    }

    public sealed class ModalRequestException : InvalidOperationException, IGameWindowDisplayException
    {
        public ModalRequestException(string errorCode, string diagnostic,
            string userMessage = "창을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.") : base(diagnostic)
        { ErrorCode = errorCode; UserMessage = userMessage; }
        public string ErrorCode { get; }
        public string UserMessage { get; }
    }

    public interface IGameWindowDisplayException
    {
        string ErrorCode { get; }
        string UserMessage { get; }
    }
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

}
