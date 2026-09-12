using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI
{
    public interface IPanelInitialLayoutParticipant
    {
        void BeginInitialContentSetup();
        bool CompleteInitialContentSetup();
        void CancelInitialContentSetup();
    }
    public enum PanelStartupState { Idle, Loading, Applying, WaitingForLayout, Revealing, Ready, Failed, Cancelled }
    public enum MissingInitialDataPolicy { ShowEmpty, PreserveAuthoredState, StayHidden }

    public readonly struct PanelLoadResult
    {
        public PanelLoadResult(bool succeeded, bool usedFallback = false, string errorCode = "")
        { Succeeded = succeeded; UsedFallback = usedFallback; ErrorCode = errorCode ?? string.Empty; }
        public bool Succeeded { get; }
        public bool UsedFallback { get; }
        public string ErrorCode { get; }
        public static PanelLoadResult Success => new(true);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class PanelStartupController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private PanelInitialDataLoader initialDataLoader;
        [SerializeField] private PanelRevealTransition revealTransition;
        [SerializeField] private RectTransform layoutRoot;
        [SerializeField] private MissingInitialDataPolicy missingDataPolicy = MissingInitialDataPolicy.ShowEmpty;
        [SerializeField] private bool autoStart = true;

        private CancellationTokenSource cancellation;
        private Task initializationTask = Task.CompletedTask;
        private IPanelInitialLayoutParticipant[] initialLayoutParticipants = Array.Empty<IPanelInitialLayoutParticipant>();
        public PanelStartupState State { get; private set; } = PanelStartupState.Idle;
        public Task WhenReady => initializationTask;

        private void Awake()
        {
            canvasGroup ??= GetComponent<CanvasGroup>();
            revealTransition?.PrepareHidden(canvasGroup);
            SetInputEnabled(false);
        }

        private void OnEnable()
        {
            if (autoStart) initializationTask = InitializeAsync();
        }

        private void OnDisable()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            if (State != PanelStartupState.Ready && State != PanelStartupState.Failed)
                State = PanelStartupState.Cancelled;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (State is PanelStartupState.Loading or PanelStartupState.Applying or
                PanelStartupState.WaitingForLayout or PanelStartupState.Revealing)
                return;

            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = cancellation.Token;
            initialLayoutParticipants = FindInitialLayoutParticipants();
            foreach (var participant in initialLayoutParticipants) participant.BeginInitialContentSetup();
            revealTransition?.PrepareHidden(canvasGroup);
            SetInputEnabled(false);
            try
            {
                if (initialDataLoader == null || !initialDataLoader.HasInitialData)
                {
                    if (missingDataPolicy == MissingInitialDataPolicy.StayHidden)
                    {
                        foreach (var participant in initialLayoutParticipants) participant.CompleteInitialContentSetup();
                        State = PanelStartupState.Ready;
                        return;
                    }
                }
                else
                {
                    State = PanelStartupState.Loading;
                    var result = await initialDataLoader.LoadAndApplyAsync(token);
                    if (!result.Succeeded) throw new InvalidOperationException(result.ErrorCode);
                    State = PanelStartupState.Applying;
                }
                State = PanelStartupState.WaitingForLayout;
                Canvas.ForceUpdateCanvases();
                if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
                foreach (var participant in initialLayoutParticipants) participant.CompleteInitialContentSetup();
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                State = PanelStartupState.Revealing;
                if (revealTransition != null) await revealTransition.RevealAsync(canvasGroup, token);
                else if (canvasGroup != null) canvasGroup.alpha = 1f;
                SetInputEnabled(true);
                State = PanelStartupState.Ready;
            }
            catch (OperationCanceledException) { foreach (var participant in initialLayoutParticipants) participant.CancelInitialContentSetup(); State = PanelStartupState.Cancelled; }
            catch (Exception exception)
            {
                foreach (var participant in initialLayoutParticipants) participant.CompleteInitialContentSetup();
                State = PanelStartupState.Failed;
                Debug.LogError($"Panel startup failed: {exception.Message}", this);
                if (missingDataPolicy != MissingInitialDataPolicy.StayHidden)
                { revealTransition?.CompleteImmediately(canvasGroup); SetInputEnabled(true); }
            }
        }

        private IPanelInitialLayoutParticipant[] FindInitialLayoutParticipants()
        {
            var root = layoutRoot != null ? layoutRoot : transform as RectTransform;
            if (root == null) return Array.Empty<IPanelInitialLayoutParticipant>();
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            var participants = new System.Collections.Generic.List<IPanelInitialLayoutParticipant>();
            foreach (var behaviour in behaviours)
                if (behaviour is IPanelInitialLayoutParticipant participant) participants.Add(participant);
            return participants.ToArray();
        }

        private void SetInputEnabled(bool enabled)
        {
            if (canvasGroup == null) return;
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }
    }
}
