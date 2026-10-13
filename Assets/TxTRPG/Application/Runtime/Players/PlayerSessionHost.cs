using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.SlotMachine;
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
        [SerializeField] private TemporarySlotMachineConfiguration temporarySlotMachineConfiguration;
        [SerializeField] private TemporaryRuneConfiguration temporaryRuneConfiguration;
        [SerializeField, Tooltip("Optional. Without it (or with an invalid one) the slot machine runs without Fortune Critical.")]
        private TemporaryFortuneCriticalConfiguration temporaryFortuneCriticalConfiguration;

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
        private bool temporarySlotCombatCreated;
        private TemporarySlotMachineState temporarySlotMachine;
        private TemporaryRuneState temporaryRunes;
        private TemporaryFortuneCriticalState temporaryFortuneCritical;
        private string temporarySlotCombatError = string.Empty;
        private TemporarySlotAcceptedAction pendingSlotAction;
        private TemporarySlotTurnRecord pendingSlotStory;
        private TemporarySlotTurnRecord lastSlotTurn;
        private TemporaryExplorationEventRecord activeExplorationEvent;
        public event Action<TemporarySlotTurnRecord> TemporarySlotStoryReady;
        // Raised when the shared combat action lock is released, so a controller created by a Scene reload can refresh.
        public event Action TemporaryCombatActionEnded;
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
        public bool IsTemporaryDiceActionPending => IsTemporaryCombatActionPending;
        // One lock for every temporary combat action, so the dice and slot machine paths never run at the same time.
        public bool IsTemporaryCombatActionPending => pendingTemporaryDiceAction != 0;
        public TemporarySlotMachineState TemporarySlotMachine => temporarySlotMachine;
        public TemporaryRuneState TemporaryRunes => temporaryRunes;
        // Session Fortune Critical rules; never null after initialization (disabled when not configured).
        public TemporaryFortuneCriticalState TemporaryFortuneCritical => temporaryFortuneCritical;
        // Why the slot machine or runes could not be created (empty when both exist). The game still starts.
        public string TemporarySlotCombatError => temporarySlotCombatError;
        public TemporarySlotAcceptedAction PendingSlotAction => pendingSlotAction;
        public TemporarySlotTurnRecord PendingSlotStory => pendingSlotStory;
        public TemporarySlotTurnRecord LastTemporarySlotTurn => lastSlotTurn;
        public TemporaryDiceTurnRecord PendingDiceResult => pendingDiceResult;
        public TemporaryDiceTurnRecord PendingDiceStory => pendingDiceStory;
        // The event node in progress (or finished and not yet reported to the exploration). Session lifetime only.
        public TemporaryExplorationEventRecord ActiveExplorationEvent => activeExplorationEvent;

        public void SetActiveExplorationEvent(TemporaryExplorationEventRecord record) =>
            activeExplorationEvent = record ?? throw new ArgumentNullException(nameof(record));

        public void ClearActiveExplorationEvent(TemporaryExplorationEventRecord record)
        {
            if (ReferenceEquals(activeExplorationEvent, record)) activeExplorationEvent = null;
        }

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

        public bool TryBeginTemporaryDiceAction(TemporaryCombatState expectedCombat, out long actionId) =>
            TryBeginTemporaryCombatAction(expectedCombat, out actionId);

        public void EndTemporaryDiceAction(long actionId) => EndTemporaryCombatAction(actionId);

        public bool TryBeginTemporaryCombatAction(TemporaryCombatState expectedCombat, out long actionId)
        {
            actionId = 0;
            if (pendingTemporaryDiceAction != 0 || expectedCombat == null ||
                !ReferenceEquals(temporaryExplorationCombat, expectedCombat)) return false;
            actionId = ++temporaryDiceActionSequence;
            if (actionId == 0) actionId = ++temporaryDiceActionSequence;
            pendingTemporaryDiceAction = actionId;
            return true;
        }

        // The same lock for a slot presentation outside combat (an exploration event slot check), so it never runs
        // together with a combat action. TryBeginTemporaryCombatAction keeps requiring the current combat.
        public bool TryBeginTemporarySlotPresentation(out long actionId)
        {
            actionId = 0;
            if (pendingTemporaryDiceAction != 0) return false;
            actionId = ++temporaryDiceActionSequence;
            if (actionId == 0) actionId = ++temporaryDiceActionSequence;
            pendingTemporaryDiceAction = actionId;
            return true;
        }

        public bool IsTemporaryActionHeld(long actionId) => actionId != 0 && pendingTemporaryDiceAction == actionId;

        public void EndTemporaryCombatAction(long actionId)
        {
            if (actionId == 0 || pendingTemporaryDiceAction != actionId) return;
            pendingTemporaryDiceAction = 0;
            TemporaryCombatActionEnded?.Invoke();
        }

        // Keeps an accepted spin until it is applied. Only the action holding the combat lock can store one.
        public void AcceptTemporarySlotAction(TemporarySlotAcceptedAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.ActionId == 0 || action.ActionId != pendingTemporaryDiceAction)
                throw new InvalidOperationException("A slot action can only be accepted while it holds the combat action lock.");
            pendingSlotAction = action;
        }

        public void ClearPendingSlotAction(TemporarySlotAcceptedAction action)
        {
            if (ReferenceEquals(pendingSlotAction, action)) pendingSlotAction = null;
        }

        public void PublishTemporarySlotStory(TemporarySlotTurnRecord record)
        {
            pendingSlotStory = lastSlotTurn = record ?? throw new ArgumentNullException(nameof(record));
            TemporarySlotStoryReady?.Invoke(record);
        }

        public void ClearPendingSlotStory(TemporarySlotTurnRecord record)
        {
            if (ReferenceEquals(pendingSlotStory, record)) pendingSlotStory = null;
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
            CreateTemporarySlotCombat();
            if (initializeOnAwake)
            {
                StartInitialization(lifetimeCancellation.Token);
            }
        }

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
        {
            CreateSession();
            CreateTemporaryDice();
            CreateTemporarySlotCombat();
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

        // Copies the configuration assets into session state once. An invalid configuration never blocks the game: the
        // state is not created, the reason is kept and the combat action stays unavailable.
        private void CreateTemporarySlotCombat()
        {
            if (temporarySlotCombatCreated) return;
            temporarySlotCombatCreated = true;
            CreateTemporaryFortuneCritical();
            if (temporarySlotMachineConfiguration == null && temporaryRuneConfiguration == null)
            {
                temporarySlotCombatError = "슬롯머신과 룬 설정이 지정되지 않았습니다.";
                return;
            }
            var reasons = string.Empty;
            if (!TemporarySlotMachineState.TryCreate(temporarySlotMachineConfiguration, out var machine, out var machineReason))
                reasons = $"Slot machine: {machineReason}";
            if (!TemporaryRuneState.TryCreate(temporaryRuneConfiguration, out var runes, out var runeReason))
                reasons += (reasons.Length > 0 ? " " : string.Empty) + $"Runes: {runeReason}";
            if (reasons.Length > 0)
            {
                temporarySlotCombatError = "슬롯머신 또는 룬 설정이 올바르지 않습니다.";
                Debug.LogError($"Temporary slot combat is unavailable. {reasons}", this);
                return;
            }
            temporarySlotMachine = machine;
            temporaryRunes = runes;
            temporarySlotCombatError = string.Empty;
        }

        // Fortune Critical is optional: a missing or invalid configuration leaves it disabled and never sets
        // TemporarySlotCombatError.
        private void CreateTemporaryFortuneCritical()
        {
            temporaryFortuneCritical = TemporaryFortuneCriticalState.Create(temporaryFortuneCriticalConfiguration,
                FortuneWeightFunctionRegistry.Default, out var configured, out var reason);
            if (configured) return;
            if (temporaryFortuneCriticalConfiguration == null)
                Debug.LogWarning($"Fortune Critical is disabled. {reason}", this);
            else
                Debug.LogError($"Fortune Critical is disabled because its configuration is invalid. {reason}", this);
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

        public void ConfigureTemporarySlotCombat(TemporarySlotMachineConfiguration slotMachine, TemporaryRuneConfiguration runes)
        {
            if (temporarySlotCombatCreated) throw new InvalidOperationException("Temporary slot combat is already initialized.");
            temporarySlotMachineConfiguration = slotMachine;
            temporaryRuneConfiguration = runes;
        }

        public void ConfigureTemporaryFortuneCritical(TemporaryFortuneCriticalConfiguration configuration)
        {
            if (temporarySlotCombatCreated) throw new InvalidOperationException("Temporary slot combat is already initialized.");
            temporaryFortuneCriticalConfiguration = configuration;
        }

#if UNITY_EDITOR
        // Edit Mode tests only: Instance is otherwise set in Awake, which runs in Play Mode.
        public static void SetInstanceForEditorTests(PlayerSessionHost host) => instance = host;
        public void UseSessionForEditorTests(PlayerSession testSession) => session = testSession ?? throw new ArgumentNullException(nameof(testSession));
        public TemporarySlotMachineConfiguration TemporarySlotMachineConfigurationForEditor => temporarySlotMachineConfiguration;
        public TemporaryRuneConfiguration TemporaryRuneConfigurationForEditor => temporaryRuneConfiguration;
        public TemporaryFortuneCriticalConfiguration TemporaryFortuneCriticalConfigurationForEditor => temporaryFortuneCriticalConfiguration;

        // Play Mode verification only: starts over with the given exploration (no combat, no event in progress). The
        // exploration controller must be initialized again to bind to it.
        public void ReplaceTemporaryExplorationForEditor(ExplorationRunState run)
        {
            temporaryExploration = run ?? throw new ArgumentNullException(nameof(run));
            temporaryExplorationCombat = null;
            activeExplorationEvent = null;
        }
#endif

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
