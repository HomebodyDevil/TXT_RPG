using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.Players;
using TxTRPG.UI;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Tests
{
    public sealed class ExplorationEventControllerTests
    {
        private readonly List<Object> created = new();
        private PlayerSessionHost host;
        private StoryTextPanel story;
        private ExplorationEventCatalog catalog;
        private GameObject nodePanel;
        private ExplorationRunState run;
        private ExplorationNodeRecord node;

        [TearDown]
        public void TearDown()
        {
            PlayerSessionHost.SetInstanceForEditorTests(null);
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        [UnityTest]
        public IEnumerator TextEvent_MovesPagesAndReportsCompletionOnce()
        {
            yield return SetUp("event.test.signpost", 30);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            var completions = Record(controller);
            controller.Begin(run, node);
            Assert.That(controller.View.TitleText, Is.EqualTo("낡은 표지판"));
            Assert.That(controller.View.Choices.Count(c => c.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(Messages(), Does.Contain("[이벤트] 낡은 표지판"));
            controller.ChooseForEditor(0);
            Assert.That(controller.View.TitleText, Is.EqualTo("표지판 뒷면"));
            controller.ChooseForEditor(0);
            Assert.That(completions, Is.EqualTo(new[] { (node.Id, ExplorationEventOutcome.Completed) }));
            Assert.That(host.ActiveExplorationEvent, Is.Null);
            Assert.That(controller.View.IsShowing, Is.False);
            controller.ChooseForEditor(0);
            Assert.That(completions.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ChoiceEvent_ShowsDisabledReasonAndAppliesEffects()
        {
            yield return SetUp("event.test.shrine", 5);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            var completions = Record(controller);
            controller.Begin(run, node);
            var offer = controller.View.Choices[1];
            Assert.That(offer.IsEnabled, Is.False);
            Assert.That(offer.Button.interactable, Is.False);
            Assert.That(offer.ReasonText, Is.EqualTo("조건 미충족: 체력 6 이상 필요"));
            controller.ChooseForEditor(1);
            Assert.That(run.PlayerHealth.Current, Is.EqualTo(5), "a disabled option does nothing");
            controller.ChooseForEditor(0);
            Assert.That((run.PlayerHealth.Current, run.PlayerHealth.Maximum), Is.EqualTo((10, 35)));
            Assert.That(controller.View.IsLocked, Is.False, "the next page unlocks the choices");
            controller.ChooseForEditor(0);
            Assert.That(completions.Single().Item2, Is.EqualTo(ExplorationEventOutcome.Completed));
            Assert.That(Messages(), Does.Contain("[이벤트] 선택: 기도한다"));
        }

        [UnityTest]
        public IEnumerator HealthZero_ReportsDefeat()
        {
            yield return SetUp("event.test.trap", 6);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            var completions = Record(controller);
            controller.Begin(run, node);
            Assert.That(completions, Is.EqualTo(new[] { (node.Id, ExplorationEventOutcome.Defeated) }));
            Assert.That(run.PlayerHealth.Current, Is.Zero);
            Assert.That(Messages(), Does.Contain("[이벤트] 체력이 0이 되었습니다."));
        }

        [UnityTest]
        public IEnumerator UnknownEvent_CompletesAsUnavailable()
        {
            yield return SetUp("event.test.signpost", 30);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            var completions = Record(controller);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not be loaded"));
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(Definition("event.test.trap"), 1) });
            controller.Configure(catalog);
            controller.Begin(run, node);
            Assert.That(controller.View.BodyText, Is.EqualTo(ExplorationEventStoryFormatter.UnavailableBody));
            controller.ChooseForEditor(0);
            Assert.That(completions, Is.EqualTo(new[] { (node.Id, ExplorationEventOutcome.Unavailable) }));
        }

        [UnityTest]
        public IEnumerator SlotCheck_IsAcceptedOnceWithFortuneAndNoRuneAllocation()
        {
            yield return SetUp("event.test.slot-trial", 20);
            Assert.That(host.TemporaryFortuneCritical.TrySetTotalChance(1d, out var reason), Is.True, reason);
            var panel = CreatePanel();
            var controller = CreateController(panel);
            var spins = new QueueRandom(5, 5, 5); // 6 + 6 + 6 = 18
            controller.SetRandomForTests(spins);
            controller.SetFortuneRandomForTests(new QueueRandom(0));
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            var completions = Record(controller);
            var runesBefore = host.TemporaryRunes.Inventory.Count;
            controller.Begin(run, node);
            controller.ChooseForEditor(0);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            var check = host.ActiveExplorationEvent.SlotCheck;
            Assert.That(check.Resolved, Is.True);
            Assert.That(spins.Calls, Is.EqualTo(3), "one spin");
            Assert.That(check.Fortune.TotalMultiplier, Is.GreaterThan(1), "the total chance 1 doubles at least");
            Assert.That(check.Fortune.FinalTotal, Is.GreaterThanOrEqualTo(36));
            Assert.That(host.ActiveExplorationEvent.Execution.CurrentPageId, Is.EqualTo("high"), "the range uses the final total");
            Assert.That(run.PlayerHealth.Current, Is.EqualTo(26));
            Assert.That(host.TemporarySlotMachine.Machine.Reels[0].Strip.Last().Value, Is.EqualTo(7));
            Assert.That(host.LastTemporarySlotTurn, Is.Null, "no combat turn");
            Assert.That(host.TemporaryRunes.Inventory.Count, Is.EqualTo(runesBefore));
            Assert.That(panel.TotalValueText, Is.EqualTo(check.Fortune.FinalTotal.ToString()));
            Assert.That(nodePanel.activeSelf, Is.True, "the node panel is shown again");
            Assert.That(Messages().Count(m => m.StartsWith("[슬롯]")), Is.EqualTo(1));
            controller.ChooseForEditor(0);
            Assert.That(completions.Single().Item2, Is.EqualTo(ExplorationEventOutcome.Completed));
            Assert.That(spins.Calls, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator SlotCheck_IsRefusedWhileAnotherActionHoldsTheLock()
        {
            yield return SetUp("event.test.slot-trial", 20);
            var controller = CreateController(CreatePanel());
            controller.SetRandomForTests(new QueueRandom(0));
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.Begin(run, node);
            Assert.That(host.TryBeginTemporarySlotPresentation(out var held), Is.True);
            Assert.That(host.TryBeginTemporarySlotPresentation(out _), Is.False, "one lock for every slot action");
            controller.ChooseForEditor(0);
            Assert.That(host.ActiveExplorationEvent.Execution.State, Is.EqualTo(ExplorationEventExecutionState.AwaitingInput));
            Assert.That(host.ActiveExplorationEvent.SlotCheck, Is.Null);
            Assert.That(Messages(), Does.Contain(ExplorationEventStoryFormatter.SlotBusy));
            host.EndTemporaryCombatAction(held);
            controller.ChooseForEditor(0);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(host.ActiveExplorationEvent.SlotCheck.Resolved, Is.True);
        }

        [UnityTest]
        public IEnumerator SceneReload_RebindsWritesPendingStoryOnceAndResolvesTheAcceptedSpinOnce()
        {
            yield return SetUp("event.test.slot-trial", 20);
            var panel = CreatePanel(5f);
            var first = CreateController(panel);
            first.SetRandomForTests(new QueueRandom(0, 0, 0)); // 1 + 1 + 1 = 3
            first.SetSpinTimeoutOverrideForEditor(.5f);
            yield return Wait(first.InitializeAsync(default, CancellationToken.None));
            first.Begin(run, node);
            first.ChooseForEditor(0);
            var record = host.ActiveExplorationEvent;
            Assert.That(record.SlotCheck, Is.Not.Null);
            Assert.That(host.IsTemporaryActionHeld(record.SlotCheck.ActionId), Is.True, "still presenting");
            story.gameObject.SetActive(false);
            Object.DestroyImmediate(first.gameObject);

            story.gameObject.SetActive(true);
            var before = story.MessageCount;
            var second = CreateController(panel);
            var secondSpins = new QueueRandom(0);
            second.SetRandomForTests(secondSpins);
            yield return Wait(second.InitializeAsync(default, CancellationToken.None));
            var completions = Record(second);
            second.Restore(run, node);
            Assert.That(host.ActiveExplorationEvent, Is.SameAs(record), "the same event, not a new pick");
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(record.SlotCheck.Resolved, Is.True);
            Assert.That(secondSpins.Calls, Is.Zero, "never spun again");
            Assert.That(record.Execution.CurrentPageId, Is.EqualTo("low"));
            Assert.That(run.PlayerHealth.Current, Is.EqualTo(16), "the -4 of the low range applied once");
            var written = story.MessageCount - before;
            Assert.That(Messages().Count(m => m.StartsWith("[슬롯]")), Is.EqualTo(1));
            second.Restore(run, node);
            Assert.That(story.MessageCount - before, Is.EqualTo(written), "pending lines are written once");
            Assert.That(run.PlayerHealth.Current, Is.EqualTo(16));
            second.ChooseForEditor(0);
            Assert.That(completions.Single().Item2, Is.EqualTo(ExplorationEventOutcome.Completed));
        }

        // ---------- Helpers ----------

        private IEnumerator SetUp(string eventId, int currentHealth)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewGameProfile>(ApplicationProjectBuilder.DefaultNewGameProfilePath);
            host = Track(new GameObject("PlayerSessionHost")).AddComponent<PlayerSessionHost>();
            host.Configure(profile, "exploration-event-tests.json", false);
            host.UseSessionForEditorTests(new PlayerSession(profile, new MemoryRepository(), new GuidCharacterInstanceIdGenerator()));
            host.ConfigureTemporarySlotCombat(Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>()),
                Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>()));
            PlayerSessionHost.SetInstanceForEditorTests(host);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Fortune Critical is disabled"));
            yield return Wait(host.EnsureInitializedAsync());
            catalog = Track(ScriptableObject.CreateInstance<ExplorationEventCatalog>());
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(Definition(eventId), 1) });
            Assert.That(catalog.TryBuild(out var runtime, out var error), Is.True, error);
            run = host.GetOrCreateTemporaryExploration(() => new ExplorationRunState("run", new HealthState(30, currentHealth),
                new ExplorationNodeGenerator(1, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Event, 1) }, new ZeroRandom(),
                    new Dictionary<string, IExplorationNodeContentPicker> { [ExplorationNodeTypeIds.Event] = runtime.CreatePicker() })));
            node = run.Select(run.CurrentChoiceSetId, run.CurrentChoices[0].Id);
            Assert.That(node.ContentId, Is.EqualTo(eventId));
            var canvas = Track(new GameObject("StoryCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            story = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<StoryTextPanel>();
            nodePanel = Track(new GameObject("ExplorationNodePanel", typeof(RectTransform)));
            nodePanel.transform.SetParent(canvas.transform, false);
            ((RectTransform)nodePanel.transform).sizeDelta = new Vector2(600f, 500f);
        }

        private ExplorationEventDefinition Definition(string id)
        {
            var definition = Track(ScriptableObject.CreateInstance<ExplorationEventDefinition>());
            ExplorationTestEventContent.Configure(definition, id);
            return definition;
        }

        private ExplorationEventController CreateController(SlotMachinePanel panel)
        {
            var view = nodePanel.transform.Find("EventView")?.GetComponent<ExplorationEventView>() ??
                       ExplorationEventViewBuilder.Build(nodePanel.transform, "EventView");
            var controller = Track(new GameObject("ExplorationEvents")).AddComponent<ExplorationEventController>();
            controller.ConfigureForEditor(view, story, panel, nodePanel);
            controller.Configure(catalog);
            return controller;
        }

        private SlotMachinePanel CreatePanel(float seconds = 0f)
        {
            var canvas = Track(new GameObject("SlotCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            var panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<SlotMachinePanel>();
            ((RectTransform)panel.transform).sizeDelta = new Vector2(360f, 280f);
            panel.SetSpinSettingsForEditor(seconds <= 0f ? SlotReelSpinSettings.Instant : new SlotReelSpinSettings(SlotEasingKind.Linear,
                0f, 0f, 0f, 0f, seconds, 0f, 0f, 0f, 0f, 0f, 18f, 8f, false, 0f, 0f, 0, 0f, 0f, 1f, 1f, 0f));
            return panel;
        }

        private static List<(string, ExplorationEventOutcome)> Record(ExplorationEventController controller)
        {
            var list = new List<(string, ExplorationEventOutcome)>();
            controller.Completed += (id, outcome) => list.Add((id, outcome));
            return list;
        }

        private IReadOnlyList<string> Messages() => story.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text).ToArray();

        private static IEnumerator Wait(Task task, double seconds = 10d)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (!task.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > deadline) Assert.Fail("The operation did not finish in time.");
                yield return null;
            }
            if (task.IsFaulted) throw task.Exception.GetBaseException();
        }

        private static IEnumerator WaitWhile(Func<bool> condition, double seconds = 10d)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            while (condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) Assert.Fail("The condition did not clear in time.");
                yield return null;
            }
        }

        private sealed class QueueRandom : IRandomIndexProvider
        {
            private readonly Queue<int> values;
            public QueueRandom(params int[] source) => values = new Queue<int>(source);
            public int Calls { get; private set; }
            public int NextIndex(int exclusiveUpperBound) { Calls++; return (values.Count > 0 ? values.Dequeue() : 0) % exclusiveUpperBound; }
        }

        private sealed class ZeroRandom : IExplorationRandomSource { public int Next(int exclusiveMaximum) => 0; }

        private sealed class MemoryRepository : IPlayerSaveRepository
        {
            private PlayerSaveData saved;
            public Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken) => Task.FromResult(saved);
            public Task SaveAsync(PlayerSaveData data, CancellationToken cancellationToken) { saved = data; return Task.CompletedTask; }
        }
    }
}
