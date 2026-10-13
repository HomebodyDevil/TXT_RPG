using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.Players;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.Exploration;

using TxTRPG.UI.SlotMachine;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode verification (exploration-event-nodes). Enter Play Mode through AppScene, then run the menu. It
    // replaces the session exploration with one whose candidates are scripted (event cards first, then a combat card),
    // plays all five test events through the real card and choice buttons, and logs EXPLORATION_EVENT_PLAY_VALIDATE.
    // Nothing is saved; the session state it changes lives only until Play Mode ends.
    public static class TemporaryExplorationEventPlayValidation
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Validate Exploration Event Nodes In Play Mode";
        private const string OtherContentScene = "Assets/Scenes/MainScene_VisualProposal.unity";
        private const string Signpost = "event.test.signpost", Shrine = "event.test.shrine", SlotTrial = "event.test.slot-trial",
            Workshop = "event.test.workshop", Trap = "event.test.trap";

        private static readonly (string Name, Vector2 Size)[] Viewports =
        {
            ("desktop 1920x1080", new Vector2(1920f, 1080f)),
            ("ultrawide 21:9", new Vector2(2520f, 1080f)),
            ("tablet 1024x768", new Vector2(1024f, 768f)),
            ("phone 390x844", new Vector2(390f, 844f))
        };

        private sealed class Refs
        {
            public PlayerSessionHost Host;
            public ExplorationRunController Run;
            public ExplorationEventController Events;
            public TemporarySlotCombatController Slot;
            public GameWindowService Windows;
            public ResponsiveMainScreen Screen;
            public ExplorationEventView View => Events.View;
            public SlotMachinePanel Panel => Slot.Panel;
            public GameObject NodePanel => Run.PanelForEditor;
            public StoryTextPanel Story => Run.StoryForEditor;
        }

        [MenuItem(MenuPath)]
        public static async void Validate()
        {
            var log = new StringBuilder("EXPLORATION_EVENT_PLAY_VALIDATE:\n");
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode from AppScene first.");
                var refs = Find();
                log.AppendLine($" scene: {refs.Run.gameObject.scene.path}");

                // 1. Event cards with title, summary and badge; signpost through pointer clicks and Submit.
                var maximum = refs.Host.TemporaryExploration.PlayerHealth.Maximum;
                await StartRun(refs, maximum, maximum, Signpost, Shrine, SlotTrial, Workshop, SlotTrial, Workshop, Workshop, Workshop, Trap);
                CheckEventCards(refs, log);
                await SelectEvent(refs, Signpost);
                Require(refs.NodePanel.activeSelf && refs.View.gameObject.activeInHierarchy, "the event screen is inside the node panel");
                Require(!refs.NodePanel.GetComponentInChildren<ExplorationNodeChoiceCardList>(true).gameObject.activeSelf, "the cards are hidden during the event");
                Require(refs.View.TitleText == "낡은 표지판", $"signpost title ({refs.View.TitleText})");
                Require(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null ||
                        refs.View.Choices.All(c => c.Button.gameObject != EventSystem.current.currentSelectedGameObject), "no choice is selected automatically");
                await Click(refs, 0, pointer: true);
                Require(refs.View.TitleText == "표지판 뒷면", "second page");
                await Submit(refs, 0);
                RequireCompleted(refs, "EventCompleted");
                Require(StoryContains(refs, "[이벤트] 낡은 표지판을(를) 마쳤습니다."), "signpost Story");
                log.AppendLine(" signpost: two pages by pointer click and Submit, completed (EventCompleted), cards back.");

                // 2. Shrine: a game window blocks the choices; the condition option costs 5 health and adds 2% Fortune chance.
                await SelectEvent(refs, Shrine);
                await CheckViewports(refs, log);
                var health = refs.Host.TemporaryExploration.PlayerHealth;
                var before = health.Current;
                var chance = refs.Host.TemporaryFortuneCritical.Rules.DefaultReelRule.Chance;
                if (refs.Windows != null)
                {
                    _ = refs.Windows.OpenAsync(GamePageIds.Inventory);
                    await WaitWhile(() => !refs.Windows.BlocksGameplayInput, 3d);
                    refs.View.Choices[1].Button.onClick.Invoke();
                    await Frames(2);
                    Require(health.Current == before && refs.View.TitleText == "수상한 제단", "a choice is ignored while a game window is open");
                    refs.Windows.Close();
                    await WaitWhile(() => refs.Windows.BlocksGameplayInput, 3d);
                    log.AppendLine(" shrine: choice ignored while the inventory window was open.");
                }
                else log.AppendLine(" shrine: GameWindowService not found; modal blocking NOT verified.");
                Require(refs.View.Choices[1].IsEnabled == before >= 6, "the offer condition follows health");
                await Click(refs, 1, pointer: true);
                Require(health.Current == before - 5, $"health {before} -> {health.Current}");
                var after = refs.Host.TemporaryFortuneCritical.Rules.DefaultReelRule.Chance;
                Require(Math.Abs(after - Math.Min(1d, chance + .02d)) < 1e-6, $"Fortune chance {chance} -> {after}");
                Require(refs.NodePanel.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains($"현재 체력 {health.Current}/")), "the status line shows the new health");
                await Click(refs, 0);
                RequireCompleted(refs, "EventCompleted");
                log.AppendLine($" shrine: offer (health {before} -> {health.Current}, default reel Fortune chance {chance:0.###} -> {after:0.###}), status line updated.");

                // 3. Slot trial: the node panel hides, the inventory window opens and closes mid-spin, the final total picks the range once.
                refs.Host.TemporaryDicePreferences.ReduceMotion = false;
                await SlotTrialRun(refs, log, window: true, reload: false);

                // 4. Workshop, first choice: one more reel.
                var reels = refs.Host.TemporarySlotMachine.ReelCount;
                await SelectEvent(refs, Workshop);
                await Click(refs, 0);
                RequireCompleted(refs, "EventCompleted");
                Require(refs.Host.TemporarySlotMachine.ReelCount == reels + 1 && refs.Panel.ReelCount == reels + 1, $"reels {reels} -> {refs.Panel.ReelCount}");
                log.AppendLine($" workshop (reel): reels {reels} -> {refs.Host.TemporarySlotMachine.ReelCount} on the panel too.");

                // 5. Slot trial again with Reduce Motion and a Scene reload mid-spin: applied once, no new spin.
                refs.Host.TemporaryDicePreferences.ReduceMotion = true;
                refs = await SlotTrialRun(refs, log, window: false, reload: true);
                refs.Host.TemporaryDicePreferences.ReduceMotion = false;

                // 6. Workshop, second and third choices: remove a 1 from the first reel, get an attack rune on a new line.
                var firstReel = refs.Host.TemporarySlotMachine.Machine.Reels[0];
                var ones = firstReel.Strip.Count(s => s.Value == 1);
                await SelectEvent(refs, Workshop);
                await Click(refs, 1);
                RequireCompleted(refs, "EventCompleted");
                var onesAfter = refs.Host.TemporarySlotMachine.Machine.Reels[0].Strip.Count(s => s.Value == 1);
                Require(ones == 0 ? onesAfter == 0 : onesAfter == ones - 1, $"ones on the first reel {ones} -> {onesAfter}");
                var lines = refs.Host.TemporaryRunes.Loadout.LineCount;
                await SelectEvent(refs, Workshop);
                await Click(refs, 2);
                RequireCompleted(refs, "EventCompleted");
                Require(refs.Host.TemporaryRunes.Loadout.LineCount == lines + 1 && refs.Panel.RuneLineTexts.Count == lines + 1, "a new rune line on the panel");
                await SelectEvent(refs, Workshop);
                await Click(refs, 2);
                RequireCompleted(refs, "EventCompleted");
                log.AppendLine($" workshop (symbol, rune x2): first reel 1s {ones} -> {onesAfter}, rune lines {lines} -> {refs.Host.TemporaryRunes.Loadout.LineCount}, panel rows {refs.Panel.RuneLineTexts.Count}.");

                // 7. Trap with enough health, then a combat that uses the changed reels, runes and Fortune rules.
                before = health.Current;
                await SelectEvent(refs, Trap);
                Require(health.Current == Math.Max(0, before - 6), "trap onEnter -6");
                if (health.Current > 0) { await Click(refs, 0); RequireCompleted(refs, "EventCompleted"); }
                log.AppendLine($" trap (health {before} -> {health.Current}): onEnter once, completed.");
                await CheckCombatUsesChanges(refs, log);

                // 8. Trap at 6 health: the exploration fails like a combat defeat.
                refs.Slot.ClearCombat();
                await StartRun(refs, maximum, 6, Trap);
                await SelectEvent(refs, Trap);
                var run = refs.Run.Run;
                Require(run.Phase == ExplorationPhase.Failed, "the exploration failed");
                Require(run.Nodes.Any(n => n.CompletionReason == "EventDefeat" && n.Status == ExplorationNodeStatus.Failed), "the node is recorded as EventDefeat");
                Require(refs.NodePanel.activeSelf && !refs.View.gameObject.activeSelf, "the failure screen replaces the event screen");
                Require(refs.NodePanel.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text == "탐험 종료: 패배"), "the failure text is shown");
                Require(StoryContains(refs, "[탐험] 체력이 0이 되어 탐험이 종료되었습니다."), "failure Story");
                log.AppendLine(" trap at 6 health: health 0, node EventDefeat, exploration failed with the defeat screen.");
                log.AppendLine(" result: PASSED. The session exploration was replaced for this check; leave Play Mode to discard it.");
                Debug.Log(log.ToString());
            }
            catch (Exception exception)
            {
                log.AppendLine($" result: FAILED: {exception.Message}");
                Debug.LogError(log + "\n" + exception);
            }
        }

        // Leaves the shrine page on screen for a screenshot (Play Mode only; nothing is saved).
        [MenuItem(MenuPath + " (Show Shrine)")]
        public static async void ShowShrine()
        {
            try
            {
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode from AppScene first.");
                var refs = Find();
                var maximum = refs.Host.TemporaryExploration.PlayerHealth.Maximum;
                refs.Slot.ClearCombat();
                await StartRun(refs, maximum, 5, Shrine);
                await SelectEvent(refs, Shrine);
                Debug.Log("EXPLORATION_EVENT_SHOW_SHRINE: shown at health 5 (the offer option is disabled with its reason).");
            }
            catch (Exception exception) { Debug.LogError($"EXPLORATION_EVENT_SHOW_SHRINE failed: {exception}"); }
        }

        private static async Task<Refs> SlotTrialRun(Refs refs, StringBuilder log, bool window, bool reload)
        {
            var host = refs.Host;
            var health = host.TemporaryExploration.PlayerHealth;
            var before = health.Current;
            var sevens = host.TemporarySlotMachine.Machine.Reels[0].Strip.Count(s => s.Value == 7);
            await SelectEvent(refs, SlotTrial);
            var record = host.ActiveExplorationEvent;
            var steps = record.Execution.AppliedStepCount;
            var storyBefore = SlotLines(refs);
            await Click(refs, 0, pointer: true);
            var check = record.SlotCheck ?? throw new InvalidOperationException("the slot check was not accepted");
            Require(!refs.NodePanel.activeSelf, "the node panel hides while the reels spin");
            var detail = string.Empty;
            if (window && refs.Windows != null)
            {
                _ = refs.Windows.OpenAsync(GamePageIds.Inventory);
                await Frames(5);
                refs.Windows.Close();
                detail = ", inventory window opened and closed mid-spin";
            }
            if (reload)
            {
                var transition = Object.FindAnyObjectByType<SceneTransitionService>() ?? throw new InvalidOperationException("SceneTransitionService not found.");
                var scenePath = refs.Run.gameObject.scene.path;
                await transition.LoadContentSceneAsync(OtherContentScene);
                Require(host.ActiveExplorationEvent == record && record.SlotCheck == check, "the accepted check is kept across the Scene change");
                await transition.LoadContentSceneAsync(scenePath);
                await Frames(3);
                refs = Find();
                detail = $", Scene reloaded mid-spin (via {System.IO.Path.GetFileNameWithoutExtension(OtherContentScene)})";
                storyBefore = 0;
            }
            await WaitWhile(() => host.IsTemporaryCombatActionPending, 15d);
            await Frames(3);
            Require(check.Resolved, "the result was reported");
            Require(record.Execution.AppliedStepCount == steps + 2, $"begin + one resolution ({record.Execution.AppliedStepCount - steps} steps)");
            var total = check.Fortune.FinalTotal;
            var expectedPage = total <= 6 ? "low" : total <= 12 ? "middle" : "high";
            Require(record.Execution.CurrentPageId == expectedPage, $"total {total} -> page {record.Execution.CurrentPageId}");
            var expectedHealth = total <= 6 ? Math.Max(0, before - 4) : total <= 12 ? before : Math.Min(health.Maximum, before + 6);
            Require(health.Current == expectedHealth, $"health {before} -> {health.Current} (expected {expectedHealth})");
            var sevensAfter = host.TemporarySlotMachine.Machine.Reels[0].Strip.Count(s => s.Value == 7);
            Require(sevensAfter == sevens + (total >= 13 ? 1 : 0), $"sevens on the first reel {sevens} -> {sevensAfter}");
            Require(refs.NodePanel.activeSelf && refs.View.gameObject.activeInHierarchy, "the node panel is back with the result page");
            Require(refs.Panel.TotalValueText == SlotCombatStoryFormatter.TotalValue(total), $"the panel shows the final total ({refs.Panel.TotalValueText})");
            Require(host.LastTemporarySlotTurn == null || host.LastTemporarySlotTurn.Spin != check.Spin, "no combat turn or rune allocation");
            Require(SlotLines(refs) - storyBefore == 1, $"one [슬롯] Story line ({SlotLines(refs) - storyBefore})");
            if (health.Current == 0)
            {
                Require(refs.Run.Run.Phase == ExplorationPhase.Failed, "defeat");
                throw new InvalidOperationException("The slot trial defeated the run; restart with more health.");
            }
            await Click(refs, 0);
            RequireCompleted(refs, "EventCompleted");
            log.AppendLine($" slot trial: {SlotCombatStoryFormatter.Spin(check.Fortune)} (Fortune {(check.Fortune.HasAnyCritical ? "critical" : "none")}) -> {expectedPage}, " +
                           $"health {before} -> {health.Current}, 7s {sevens} -> {sevensAfter}, applied once{detail}{(host.TemporaryDicePreferences.ReduceMotion ? ", Reduce Motion" : string.Empty)}.");
            return refs;
        }

        private static async Task CheckCombatUsesChanges(Refs refs, StringBuilder log)
        {
            var run = refs.Run.Run;
            var combat = run.CurrentChoices.FirstOrDefault(n => n.TypeId == ExplorationNodeTypeIds.Combat) ?? throw new InvalidOperationException("No combat card.");
            await SelectCard(refs, combat);
            Require(refs.Slot.Combat != null, "the combat started");
            var action = refs.Slot.ActionButtonForEditor;
            await WaitWhile(() => !action.IsInteractable, 5d);
            var before = refs.Host.LastTemporarySlotTurn;
            ExecuteEvents.Execute(action.Button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            await WaitWhile(() => refs.Host.IsTemporaryCombatActionPending || refs.Host.LastTemporarySlotTurn == before, 15d);
            var turn = refs.Host.LastTemporarySlotTurn;
            Require(turn != null && turn != before, "one combat turn");
            Require(turn.Spin.Reels.Count == refs.Host.TemporarySlotMachine.ReelCount, $"the spin used {turn.Spin.Reels.Count} reels");
            Require(turn.Allocation.Lines.Count == refs.Host.TemporaryRunes.Loadout.LineCount && turn.Allocation.Lines.Any(l => l.LineId.StartsWith("line.event.")),
                "the allocation used the event rune lines");
            log.AppendLine($" next combat: spin with {turn.Spin.Reels.Count} reels ({string.Join(", ", turn.Spin.Reels.Select(r => r.ReelId))}), " +
                           $"allocation over {turn.Allocation.Lines.Count} lines incl. {string.Join(", ", turn.Allocation.Lines.Where(l => l.LineId.StartsWith("line.event.")).Select(l => l.LineId))}, " +
                           $"Fortune default reel chance {refs.Host.TemporaryFortuneCritical.Rules.DefaultReelRule.Chance:0.###}.");
        }

        private static void CheckEventCards(Refs refs, StringBuilder log)
        {
            var list = refs.NodePanel.GetComponentInChildren<ExplorationNodeChoiceCardList>(true);
            var texts = list.Cards.Where(c => c.gameObject.activeSelf).Select(c => string.Join("|", c.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text))).ToArray();
            Require(texts.Any(t => t.Contains("낡은 표지판") && t.Contains("갈림길에 오래된 표지판이 서 있다.") && t.Contains("이벤트")), "an event card shows title, summary and badge");
            Require(texts.Any(t => t.Contains("전투")), "the combat card is still offered");
            var tree = Object.FindAnyObjectByType<ExplorationNodeTreePanel>();
            log.AppendLine($" cards: {texts.Length} ({string.Join(" / ", texts.Select(t => t.Split('|').FirstOrDefault(s => s.Length > 0)))}); tree panel {(tree != null ? "present" : "missing")}.");
        }

        private static async Task CheckViewports(Refs refs, StringBuilder log)
        {
            if (refs.Screen == null || refs.Screen.PageScroll == null) { log.AppendLine(" viewports: ResponsiveMainScreen missing; NOT verified."); return; }
            var viewport = refs.Screen.PageScroll.viewport;
            var saved = (viewport.anchorMin, viewport.anchorMax, viewport.sizeDelta, viewport.anchoredPosition);
            try
            {
                foreach (var (name, size) in Viewports)
                {
                    viewport.anchorMin = viewport.anchorMax = new Vector2(.5f, .5f);
                    viewport.sizeDelta = size; viewport.anchoredPosition = Vector2.zero;
                    await Task.Delay(250);
                    Canvas.ForceUpdateCanvases();
                    var view = (RectTransform)refs.View.transform;
                    var viewRect = WorldRect(view);
                    var title = refs.View.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Title");
                    var body = refs.View.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Body");
                    var choicesScroll = refs.View.GetComponentsInChildren<ScrollRect>(true).First(s => s.name == "ChoicesScroll");
                    var bodyScroll = refs.View.GetComponentsInChildren<ScrollRect>(true).First(s => s.name == "BodyScroll");
                    Require(Inside(viewRect, WorldRect(title.rectTransform)), $"{name}: title inside the view");
                    Require(title.preferredHeight <= title.rectTransform.rect.height + 1f, $"{name}: title fits ({title.preferredHeight:F0} > {title.rectTransform.rect.height:F0})");
                    Require(Inside(viewRect, WorldRect((RectTransform)choicesScroll.transform)), $"{name}: choices inside the view");
                    Require(bodyScroll.viewport.rect.height >= 40f, $"{name}: body area {bodyScroll.viewport.rect.height:F0}");
                    var choices = refs.View.Choices.Where(c => c.gameObject.activeSelf).ToArray();
                    Require(choices.All(c => ((RectTransform)c.transform).rect.height >= 48f), $"{name}: touch targets at least 48");
                    var contentHeight = choicesScroll.content.rect.height;
                    var reachable = contentHeight <= choicesScroll.viewport.rect.height + 1f || choicesScroll.vertical;
                    Require(reachable, $"{name}: every choice reachable");
                    var bodyReachable = body.rectTransform.rect.height <= bodyScroll.viewport.rect.height + 1f || bodyScroll.vertical;
                    Require(bodyReachable, $"{name}: body reachable");
                    log.AppendLine($" {name}: event view {view.rect.width:F0}x{view.rect.height:F0}, body area {bodyScroll.viewport.rect.height:F0}, choices area {choicesScroll.viewport.rect.height:F0} / content {contentHeight:F0}, choice height {((RectTransform)choices[0].transform).rect.height:F0}.");
                }
            }
            finally
            {
                viewport.anchorMin = saved.anchorMin; viewport.anchorMax = saved.anchorMax;
                viewport.sizeDelta = saved.sizeDelta; viewport.anchoredPosition = saved.anchoredPosition;
                await Task.Delay(250);
            }
        }

        // ---------- Driving the UI ----------

        // Each choice set offers [target event, another event, combat]; the targets come in the given order.
        private static async Task StartRun(Refs refs, int maximum, int current, params string[] targets)
        {
            var picker = new ScriptedPicker(targets.SelectMany(id => new[] { id, id == Signpost ? Shrine : Signpost }));
            // Weights combat 1 / event 1 and draws 1, 1, 0: every choice set is [event, event, combat].
            var generator = new ExplorationNodeGenerator(3, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 1), new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 1) },
                new CycleRandom(1, 1, 0), new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = picker });
            refs.Host.ReplaceTemporaryExplorationForEditor(new ExplorationRunState(Guid.NewGuid().ToString("N"), new HealthState(maximum, current), generator));
            await refs.Run.InitializeAsync(default, CancellationToken.None);
            await Frames(2);
        }

        private static async Task SelectEvent(Refs refs, string eventId)
        {
            var node = refs.Run.Run.CurrentChoices.FirstOrDefault(n => n.ContentId == eventId) ?? throw new InvalidOperationException($"No '{eventId}' card.");
            await SelectCard(refs, node);
            Require(refs.Run.Run.ActiveNodeId == node.Id || refs.Run.Run.Phase != ExplorationPhase.ResolvingNode, $"{eventId} selected");
        }

        // Through the card button (the same path as a click), including its confirmation feedback.
        private static async Task SelectCard(Refs refs, ExplorationNodeRecord node)
        {
            var list = refs.NodePanel.GetComponentInChildren<ExplorationNodeChoiceCardList>(true);
            var card = list.Cards.First(c => c.gameObject.activeSelf && c.Request.NodeId == node.Id);
            var phase = refs.Run.Run.Phase;
            ExecuteEvents.Execute(card.Button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            await WaitWhile(() => refs.Run.Run.Phase == phase && refs.Run.Run.CurrentChoices.Any(n => n.Id == node.Id), 5d);
            await Frames(2);
        }

        private static async Task Click(Refs refs, int index, bool pointer = false)
        {
            var button = refs.View.Choices[index].Button;
            Require(button.gameObject.activeInHierarchy && button.IsInteractable(), $"choice {index} is usable");
            if (pointer) ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            else button.onClick.Invoke();
            await Frames(2);
        }

        // Keyboard/gamepad Submit on the focused choice.
        private static async Task Submit(Refs refs, int index)
        {
            var button = refs.View.Choices[index].Button;
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            await Frames(2);
        }

        private static void RequireCompleted(Refs refs, string reason)
        {
            var run = refs.Run.Run;
            Require(run.Phase == ExplorationPhase.AwaitingChoice, $"the event finished ({run.Phase})");
            var last = run.Nodes.Last(n => n.Id == run.SelectedPath.Last());
            Require(last.CompletionReason == reason, $"completion reason {last.CompletionReason}");
            Require(!refs.View.gameObject.activeSelf && refs.Host.ActiveExplorationEvent == null, "the event screen closed");
        }

        private static int SlotLines(Refs refs) => StoryTexts(refs).Count(t => t.StartsWith("[슬롯]", StringComparison.Ordinal));
        private static bool StoryContains(Refs refs, string text) => StoryTexts(refs).Contains(text);
        private static IEnumerable<string> StoryTexts(Refs refs) => refs.Story.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text);

        private static Refs Find()
        {
            var run = Object.FindAnyObjectByType<ExplorationRunController>() ?? throw new InvalidOperationException("ExplorationRunController is missing.");
            return new Refs
            {
                Host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("The player session is missing."),
                Run = run,
                Events = run.EventControllerForEditor as ExplorationEventController ?? throw new InvalidOperationException("ExplorationEventController is not linked."),
                Slot = run.CombatControllerForEditor as TemporarySlotCombatController ?? throw new InvalidOperationException("TemporarySlotCombatController is missing."),
                Windows = (run.EventControllerForEditor as ExplorationEventController)?.View?.WindowService,
                Screen = Object.FindAnyObjectByType<ResponsiveMainScreen>()
            };
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool Inside(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin - 1f && inner.xMax <= outer.xMax + 1f && inner.yMin >= outer.yMin - 1f && inner.yMax <= outer.yMax + 1f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static Task Frames(int count) => TemporarySlotMachinePlayValidation.Frames(count);
        private static Task WaitWhile(Func<bool> condition, double seconds) => TemporarySlotMachinePlayValidation.WaitWhile(condition, seconds);

        private sealed class ScriptedPicker : IExplorationNodeContentPicker
        {
            private readonly Queue<string> ids;
            public ScriptedPicker(IEnumerable<string> source) => ids = new Queue<string>(source);
            public bool TryPick(ExplorationNodeContentRequest request, IExplorationRandomSource random, out string contentId)
            {
                contentId = ids.Count > 0 ? ids.Dequeue() : Signpost;
                return true;
            }
        }

        private sealed class CycleRandom : IExplorationRandomSource
        {
            private readonly int[] values;
            private int index;
            public CycleRandom(params int[] source) => values = source;
            public int Next(int exclusiveMaximum) => values[index++ % values.Length] % exclusiveMaximum;
        }
    }
}
