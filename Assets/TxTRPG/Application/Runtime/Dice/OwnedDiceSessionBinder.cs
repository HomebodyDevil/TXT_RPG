using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Dice;
using TxTRPG.SceneTransition;
using TxTRPG.UI.Dice;
using TxTRPG.UI.Windows;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TxTRPG.Application.Dice
{
    [DisallowMultipleComponent]
    public sealed class OwnedDiceSessionBinder : MonoBehaviour, ISceneInitializer
    {
        public const string ResultsPageId = "dice-results";
        [SerializeField] private OwnedDicePanel panel;
        [SerializeField] private GameWindowService windowService;
        [SerializeField] private DiceResultsGameWindowPage resultsPage;
        private PlayerSessionHost session;
        private TemporaryDiceTurnRecord pendingRecord;
        private GameObject pendingFocus;
        private bool opening;
        public int InitializationOrder => -810;
        public bool BlocksNextAction => pendingRecord != null || opening ||
            windowService != null && windowService.BlocksGameplayInput;
        public OwnedDicePanel Panel => panel;
        public event Action BlockingChanged;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            Unbind();
            session = PlayerSessionHost.Instance ?? throw new InvalidOperationException("Owned dice requires PlayerSessionHost.");
            await session.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (panel == null || panel.ResultsButton == null || windowService == null || resultsPage == null ||
                !windowService.HasPage(ResultsPageId))
                throw new InvalidOperationException("Owned dice panel or results page references are incomplete.");
            session.TemporaryDice.Changed += RefreshDice;
            session.TemporaryDiceResultReady += OnDiceResultReady;
            windowService.Closed += OnWindowClosed;
            panel.ResultsButton.onClick.AddListener(OpenFromButton);
            RefreshDice();
            panel.SetResultsAvailable(true);
            if (session.PendingDiceResult != null) OnDiceResultReady(session.PendingDiceResult);
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (session?.TemporaryDice != null) session.TemporaryDice.Changed -= RefreshDice;
            if (session != null) session.TemporaryDiceResultReady -= OnDiceResultReady;
            if (windowService != null) windowService.Closed -= OnWindowClosed;
            if (panel != null && panel.ResultsButton != null)
                panel.ResultsButton.onClick.RemoveListener(OpenFromButton);
            pendingRecord = null;
            pendingFocus = null;
            session = null;
        }

        private void RefreshDice()
        {
            if (session?.TemporaryDice == null || panel == null) return;
            var snapshot = session.TemporaryDice.Snapshot();
            var display = new OwnedDieDisplay[snapshot.Count];
            for (var i = 0; i < display.Length; i++)
                display[i] = new OwnedDieDisplay(snapshot[i].InstanceId, snapshot[i].DefinitionId,
                    snapshot[i].DisplayName, snapshot[i].Faces);
            panel.SetDice(display);
        }

        public void RequestAutomaticResults(TemporaryDiceTurnRecord record, GameObject returnFocus)
        {
            if (record == null || session == null) return;
            if (!session.TemporaryDicePreferences.AutoShowResults)
            { session.ClearPendingDiceResult(record); return; }
            pendingRecord = record;
            pendingFocus = returnFocus;
            BlockingChanged?.Invoke();
            if (!windowService.IsOpen) _ = OpenPendingAsync();
        }

        private void OnDiceResultReady(TemporaryDiceTurnRecord record) =>
            RequestAutomaticResults(record, EventSystem.current?.currentSelectedGameObject);

        private async Task OpenPendingAsync()
        {
            if (opening || pendingRecord == null || windowService == null || windowService.IsOpen) return;
            var record = pendingRecord;
            if (session == null || PlayerSessionHost.Instance != session ||
                !ContainsRecord(session.TemporaryDiceHistory.Snapshot(), record))
            {
                session?.ClearPendingDiceResult(record);
                pendingRecord = null;
                BlockingChanged?.Invoke();
                return;
            }
            opening = true;
            try
            {
                resultsPage.SetRows(BuildRows(session.TemporaryDiceHistory.Snapshot()));
                var request = windowService.CreateRequest(ResultsPageId, pendingFocus);
                if (request == null || !await windowService.OpenAsync(request))
                    Debug.LogWarning("Dice results could not be opened; the recorded result remains available.", this);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Dice results display failed: {exception.Message}", this);
            }
            finally
            {
                if (ReferenceEquals(record, pendingRecord)) pendingRecord = null;
                session?.ClearPendingDiceResult(record);
                pendingFocus = null;
                opening = false;
                BlockingChanged?.Invoke();
            }
        }

        private void OnWindowClosed()
        {
            BlockingChanged?.Invoke();
            if (pendingRecord != null) _ = OpenPendingAsync();
        }

        private void OpenFromButton()
        {
            if (session == null || windowService == null || windowService.IsOpen ||
                session.IsTemporaryDiceActionPending) return;
            _ = OpenManualAsync();
        }

        private async Task OpenManualAsync()
        {
            opening = true;
            BlockingChanged?.Invoke();
            try
            {
                resultsPage.SetRows(BuildRows(session.TemporaryDiceHistory.Snapshot()));
                var focus = EventSystem.current?.currentSelectedGameObject ?? panel.ResultsButton.gameObject;
                var request = windowService.CreateRequest(ResultsPageId, focus);
                if (request != null) await windowService.OpenAsync(request);
            }
            catch (Exception exception) { Debug.LogWarning($"Dice results display failed: {exception.Message}", this); }
            finally { opening = false; BlockingChanged?.Invoke(); }
        }

        private static bool ContainsRecord(IReadOnlyList<TemporaryDiceTurnRecord> records, TemporaryDiceTurnRecord record)
        {
            foreach (var item in records) if (ReferenceEquals(item, record)) return true;
            return false;
        }

        public static IReadOnlyList<string> BuildRows(IReadOnlyList<TemporaryDiceTurnRecord> records)
        {
            var rows = new List<string>(records.Count);
            foreach (var record in records)
            {
                var lines = new List<string>
                {
                    $"전투 노드 {record.NodeId} · {record.Order}번째 행동"
                };
                for (var i = 0; i < record.Rolls.Count; i++)
                {
                    var roll = record.Rolls[i];
                    var result = roll.Result;
                    var effect = result.EffectKind == DiceEffectKind.Attack ? "공격" :
                        result.EffectKind == DiceEffectKind.Heal ? "회복" : "알 수 없음";
                    var applied = i < record.Events.Count ? record.Events[i] : default;
                    var reason = applied.Kind == TemporaryCombatEventKind.PlayerAttackSkipped ? "적 처치로 건너뜀" :
                        $"실제 적용 {applied.AppliedAmount}";
                    lines.Add($"{roll.Order}. {roll.DisplayName} ({roll.DefinitionId}, 면 {result.FaceIndex + 1}/{roll.FaceCount}): " +
                        $"{effect} {result.Amount}, {reason}");
                }
                for (var i = record.Rolls.Count; i < record.Events.Count; i++)
                {
                    var item = record.Events[i];
                    lines.Add(item.Kind switch
                    {
                        TemporaryCombatEventKind.EnemyAttack => $"적 행동: 공격 {item.RequestedAmount}, 실제 피해 {item.AppliedAmount}",
                        TemporaryCombatEventKind.EnemyHeal => $"적 행동: 회복 {item.RequestedAmount}, 실제 회복 {item.AppliedAmount}",
                        TemporaryCombatEventKind.Victory => "전투 종료: 승리",
                        TemporaryCombatEventKind.Defeat => "전투 종료: 패배",
                        _ => item.Kind.ToString()
                    });
                }
                rows.Add(string.Join("\n", lines));
            }
            return rows;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(OwnedDicePanel ownedPanel, GameWindowService service,
            DiceResultsGameWindowPage page)
        { panel = ownedPanel; windowService = service; resultsPage = page; }
#endif
    }
}
