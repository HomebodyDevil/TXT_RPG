using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Dice;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Exploration;
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
        private readonly TemporaryDiceRollHistory temporaryDiceHistory = new();
        private readonly TemporaryDiceResultPreferences temporaryDicePreferences = new();
        private ExplorationRunState temporaryExploration;
        private TemporaryCombatState temporaryExplorationCombat;
        private long temporaryDiceActionSequence;
        private long pendingTemporaryDiceAction;
        private TemporaryDiceTurnRecord pendingDiceResult;
        private TemporaryDiceTurnRecord pendingDiceStory;
        public event Action<TemporaryCombatState, bool> TemporaryCombatCompleted;
        public event Action<TemporaryDiceTurnRecord> TemporaryDiceResultReady;
        public event Action<TemporaryDiceTurnRecord> TemporaryDiceStoryReady;

        public static PlayerSessionHost Instance => instance;
        public IPlayerSession Session => session;
        public bool IsReady => session != null && session.IsReady;
        public Task WhenReady => EnsureInitializedAsync();
        public TemporaryPlayerDiceState TemporaryDice => temporaryDice;
        public TemporaryDiceRollHistory TemporaryDiceHistory => temporaryDiceHistory;
        public TemporaryDiceResultPreferences TemporaryDicePreferences => temporaryDicePreferences;
        public ExplorationRunState TemporaryExploration => temporaryExploration;
        public TemporaryCombatState TemporaryExplorationCombat => temporaryExplorationCombat;
        public bool IsTemporaryDiceActionPending => pendingTemporaryDiceAction != 0;
        public TemporaryDiceTurnRecord PendingDiceResult => pendingDiceResult;
        public TemporaryDiceTurnRecord PendingDiceStory => pendingDiceStory;

        public void PublishTemporaryDiceStory(TemporaryDiceTurnRecord record)
        {
            pendingDiceStory = record ?? throw new ArgumentNullException(nameof(record));
            TemporaryDiceStoryReady?.Invoke(record);
        }

        public void ClearPendingDiceStory(TemporaryDiceTurnRecord record)
        {
            if (ReferenceEquals(pendingDiceStory, record)) pendingDiceStory = null;
        }

        public void NotifyTemporaryCombatCompleted(TemporaryCombatState completedCombat, bool victory)
        {
            TemporaryCombatCompleted?.Invoke(completedCombat, victory);
        }

        public void QueueTemporaryDiceResult(TemporaryDiceTurnRecord record)
        {
            pendingDiceResult = record ?? throw new ArgumentNullException(nameof(record));
            TemporaryDiceResultReady?.Invoke(record);
        }

        public void ClearPendingDiceResult(TemporaryDiceTurnRecord record)
        {
            if (ReferenceEquals(pendingDiceResult, record)) pendingDiceResult = null;
        }

        public bool TryBeginTemporaryDiceAction(TemporaryCombatState expectedCombat, out long actionId)
        {
            actionId = 0;
            if (pendingTemporaryDiceAction != 0 || expectedCombat == null ||
                !ReferenceEquals(temporaryExplorationCombat, expectedCombat)) return false;
            actionId = ++temporaryDiceActionSequence;
            if (actionId == 0) actionId = ++temporaryDiceActionSequence;
            pendingTemporaryDiceAction = actionId;
            return true;
        }

        public void EndTemporaryDiceAction(long actionId)
        {
            if (actionId != 0 && pendingTemporaryDiceAction == actionId)
                pendingTemporaryDiceAction = 0;
        }

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

        public ExplorationRunState GetOrCreateTemporaryExploration(Func<ExplorationRunState> factory)
        {
            if (temporaryExploration != null) return temporaryExploration;
            temporaryExploration = factory?.Invoke() ?? throw new ArgumentNullException(nameof(factory));
            return temporaryExploration;
        }

        public void SetTemporaryExplorationCombat(TemporaryCombatState combat)
        {
            temporaryExplorationCombat = combat;
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
