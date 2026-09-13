using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Dice;
using UnityEngine;

namespace TxTRPG.Application.Players
{
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class PlayerSessionHost : MonoBehaviour
    {
        private static PlayerSessionHost instance;

        [SerializeField] private NewGameProfile newGameProfile;
        [SerializeField] private string saveFileName = "player-save.json";
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private TemporaryDiceConfiguration temporaryDiceConfiguration;

        private CancellationTokenSource lifetimeCancellation;
        private PlayerSession session;
        private Task initializationTask;
        private TemporaryPlayerDiceState temporaryDice;

        public static PlayerSessionHost Instance => instance;
        public IPlayerSession Session => session;
        public bool IsReady => session != null && session.IsReady;
        public Task WhenReady => EnsureInitializedAsync();
        public TemporaryPlayerDiceState TemporaryDice => temporaryDice;

        private void Awake()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                return;
            }
            if (instance != null && instance != this)
            {
                Debug.LogError(
                    "More than one PlayerSessionHost is active. The duplicate will be removed.",
                    this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            lifetimeCancellation = new CancellationTokenSource();
            CreateSession();
            CreateTemporaryDice();
            if (initializeOnAwake)
            {
                StartInitialization(lifetimeCancellation.Token);
            }
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            CreateSession();
            CreateTemporaryDice();
            if (initializationTask == null ||
                initializationTask.IsCanceled ||
                initializationTask.IsFaulted)
            {
                var token = lifetimeCancellation != null
                    ? lifetimeCancellation.Token
                    : cancellationToken;
                StartInitialization(token);
            }
            return initializationTask;
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync(cancellationToken);
            await session.SaveAsync(cancellationToken);
        }

        public void Configure(
            NewGameProfile profile,
            string configuredSaveFileName = "player-save.json",
            bool initializeAutomatically = true)
        {
            newGameProfile = profile;
            saveFileName = NormalizeSaveFileName(configuredSaveFileName);
            initializeOnAwake = initializeAutomatically;
        }

        private void CreateSession()
        {
            if (session != null)
            {
                return;
            }
            if (newGameProfile == null)
            {
                throw new InvalidOperationException(
                    "PlayerSessionHost requires a NewGameProfile.");
            }

            var normalizedFileName = NormalizeSaveFileName(saveFileName);
            var repository = new LocalPlayerSaveRepository(
                Path.Combine(
                    UnityEngine.Application.persistentDataPath,
                    normalizedFileName));
            session = new PlayerSession(
                newGameProfile,
                repository,
                new GuidCharacterInstanceIdGenerator());
        }

        private void CreateTemporaryDice()
        {
            if (temporaryDice != null || temporaryDiceConfiguration == null) return;
            temporaryDice = new TemporaryPlayerDiceState(temporaryDiceConfiguration);
        }

        private void StartInitialization(CancellationToken cancellationToken)
        {
            initializationTask = session.InitializeAsync(cancellationToken);
            _ = ReportFailureAsync(initializationTask);
        }

        private async Task ReportFailureAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException) when (
                lifetimeCancellation != null &&
                lifetimeCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Player session initialization failed: {exception.Message}",
                    this);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        public void ConfigureTemporaryDice(TemporaryDiceConfiguration configuration)
        {
            if (temporaryDice != null) throw new InvalidOperationException("Temporary dice are already initialized.");
            temporaryDiceConfiguration = configuration;
        }

        private static string NormalizeSaveFileName(string value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized) ||
                !string.Equals(Path.GetFileName(normalized), normalized, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Player save file name must be a file name without directory segments.",
                    nameof(value));
            }
            return normalized;
        }
    }
}
