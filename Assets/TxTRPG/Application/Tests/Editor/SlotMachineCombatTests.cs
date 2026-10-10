using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Players;
using TxTRPG.Application.Runes;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Combat;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Players;
using TxTRPG.Gameplay.Runes;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Tests
{
    public sealed class SlotMachineCombatTests
    {
        private readonly List<Object> created = new();
        private PlayerSessionHost host;
        private StoryTextPanel story;
        private TemporaryCombatConfiguration combatConfiguration;
        private CountingRandom random;

        [TearDown]
        public void TearDown()
        {
            PlayerSessionHost.SetInstanceForEditorTests(null);
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        // ---------- Configuration and session ----------

        [Test]
        public void Configuration_IsCopiedAndTheAssetIsNotChanged()
        {
            var slots = Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>());
            var runes = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            Assert.That(TemporarySlotMachineState.TryCreate(slots, out var slotState, out var reason), Is.True, reason);
            Assert.That(TemporaryRuneState.TryCreate(runes, out var runeState, out reason), Is.True, reason);
            Assert.That(slotState.Machine.Reels.Select(r => r.Id), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3" }));
            Assert.That(slotState.Machine.Reels.All(r => r.Strip.Select(s => s.Value).SequenceEqual(new[] { 1, 2, 3, 4, 5, 6 })), Is.True);
            Assert.That(runeState.Loadout.Lines.Select(l => (l.LineId, l.Weight)), Is.EqualTo(new[] { ("line.1", 1), ("line.2", 1) }));

            var changes = 0;
            slotState.Changed += () => changes++;
            runeState.Changed += () => changes++;
            Assert.That(slotState.Machine.TryAddReel(SlotReel.FromValues("reel.4", new[] { 7 }), out reason), Is.True, reason);
            Assert.That(slotState.Machine.TrySetSymbol("reel.1", 0, 9, out reason), Is.True, reason);
            Assert.That(runeState.Inventory.TryAdd(new OwnedRune("rune.attack.2", TemporaryRuneConfiguration.AttackRuneId), out reason), Is.True, reason);
            Assert.That(runeState.Loadout.TryAddRuneToLine("line.1", "rune.attack.2", out reason), Is.True, reason);
            Assert.That(changes, Is.EqualTo(4));
            Assert.That(slots.Reels.Count, Is.EqualTo(3));
            Assert.That(slots.Reels[0].Values[0], Is.EqualTo(1));
            Assert.That(runes.StartingRunes.Count, Is.EqualTo(2));
            Assert.That(runes.StartingLines[0].RuneInstanceIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidConfiguration_IsReportedWithoutThrowing()
        {
            var slots = Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>());
            slots.ConfigureForEditor(new[] { new TemporarySlotReelDefinition("reel.1"), new TemporarySlotReelDefinition("reel.1", 1) });
            Assert.That(TemporarySlotMachineState.TryCreate(slots, out var state, out var reason), Is.False);
            Assert.That(state, Is.Null);
            Assert.That(reason, Is.Not.Empty);
            var runes = Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>());
            runes.ConfigureForEditor(
                new[] { new TemporaryRuneDefinitionEntry("rune.attack", "공격 룬", RuneEffectKind.Attack) },
                new[] { new TemporaryOwnedRuneEntry("a1", "rune.attack"), new TemporaryOwnedRuneEntry("h1", "rune.heal") },
                Array.Empty<TemporaryRuneLineEntry>());
            Assert.That(TemporaryRuneState.TryCreate(runes, out var runeState, out reason), Is.False);
            Assert.That(runeState, Is.Null);
            Assert.That(TemporarySlotMachineState.TryCreate(null, out _, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator InvalidConfiguration_DoesNotBlockTheSessionAndDisablesTheAction()
        {
            var slots = Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>());
            slots.ConfigureForEditor(Array.Empty<TemporarySlotReelDefinition>());
            CreateHost(slots, Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>()));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Temporary slot combat is unavailable"));
            var initialization = host.EnsureInitializedAsync();
            yield return Wait(initialization);
            Assert.That(initialization.IsFaulted, Is.False);
            Assert.That(host.IsReady, Is.True, "the game still starts");
            Assert.That(host.TemporarySlotMachine, Is.Null);
            Assert.That(host.TemporarySlotCombatError, Is.Not.Empty);
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            Assert.That(controller.IsReady, Is.False);
            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        // ---------- Controller ----------

        [UnityTest]
        public IEnumerator OneAction_SpinsOnceAllocatesAndExecutesTheTurnOnce()
        {
            yield return SetUpCombat();
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var combat = controller.Combat;
            random.Values.Enqueue(2); random.Values.Enqueue(4); random.Values.Enqueue(1); // 3 + 5 + 2 = 10
            var before = story.MessageCount;

            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out var reason), Is.True, reason);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);

            Assert.That(random.Calls, Is.EqualTo(3), "one draw per reel, one spin");
            var turn = host.LastTemporarySlotTurn;
            Assert.That(turn.Spin.PaylineTotal, Is.EqualTo(10));
            Assert.That(turn.Allocation.Lines.Select(l => l.Amount), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(35), "attack line 5 applied once");
            Assert.That(combat.PlayerHealth.Current, Is.EqualTo(25), "heal at full health, then the enemy attack 5");
            Assert.That(combat.NextEnemyAction, Is.EqualTo(EnemyActionKind.Heal));
            Assert.That(story.MessageCount - before, Is.EqualTo(6), "one Story record of six lines");
            Assert.That(SlotCombatStoryFormatter.Turn(turn, combatConfiguration.EnemyName), Is.EqualTo(new[]
            {
                "[슬롯] 3 + 5 + 2 = 10",
                "[룬] 1번 줄 공격 룬 ×1: 5",
                "[전투] 테스트 적에게 피해 5. 체력 35/40.",
                "[룬] 2번 줄 회복 룬 ×1: 5",
                "[전투] 플레이어 회복 요청 5, 실제 회복 0. 체력 30/30.",
                "[적 행동] 공격 5. 플레이어가 받은 피해 5. 체력 25/30."
            }));
            Assert.That(host.PendingSlotAction, Is.Null);
            Assert.That(host.PendingSlotStory, Is.Null);
        }

        [UnityTest]
        public IEnumerator DuplicateExecution_IsRejectedWhileTheResultIsProcessed()
        {
            yield return SetUpCombat();
            var panel = CreatePanel(10f);
            var controller = CreateController(panel);
            controller.SetSpinTimeoutOverrideForEditor(.3f);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var combat = controller.Combat;
            EnqueueSpins(1);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            Assert.That(host.PendingSlotAction, Is.Not.Null, "the accepted spin is kept by the session");
            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out _), Is.False);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already being processed"));
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.False);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SLOT_SPIN_TIMEOUT"));
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(random.Calls, Is.EqualTo(3));
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(40 - host.LastTemporarySlotTurn.Allocation.Lines[0].Amount));
            Assert.That(panel.IsSpinning, Is.False);
            Assert.That(panel.TotalText, Is.EqualTo(SlotCombatStoryFormatter.Total(host.LastTemporarySlotTurn.Spin.PaylineTotal)), "the timeout shows the accepted result");
        }

        [UnityTest]
        public IEnumerator NoRuneLine_CannotExecute()
        {
            yield return SetUpCombat();
            var controller = CreateController(null);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var loadout = host.TemporaryRunes.Loadout;
            Assert.That(loadout.TryRemoveLine("line.1", out var reason), Is.True, reason);
            Assert.That(loadout.TryRemoveLine("line.2", out reason), Is.True, reason);
            Assert.That(controller.CanExecute(TemporarySlotCombatController.SpinCommandId, out reason), Is.False);
            Assert.That(reason, Does.Contain("rune"));
        }

        [UnityTest]
        public IEnumerator ChangedCombatAfterAcceptance_IsNotApplied()
        {
            yield return SetUpCombat();
            var panel = CreatePanel(10f);
            var controller = CreateController(panel);
            controller.SetSpinTimeoutOverrideForEditor(.2f);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            var accepted = controller.Combat;
            EnqueueSpins(1);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            var replacement = new TemporaryCombatState(new HealthState(30, 30), 40, 5, 3);
            host.SetTemporaryExplorationCombat(replacement);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SLOT_SPIN_TIMEOUT"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("previous session or combat"));
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(accepted.EnemyHealth.Current, Is.EqualTo(40));
            Assert.That(replacement.EnemyHealth.Current, Is.EqualTo(40));
            Assert.That(host.LastTemporarySlotTurn, Is.Null);
        }

        [UnityTest]
        public IEnumerator SceneReinitialization_AppliesTheAcceptedResultExactlyOnce()
        {
            yield return SetUpCombat();
            var panel = CreatePanel(10f);
            var first = CreateController(panel);
            first.SetSpinTimeoutOverrideForEditor(.3f);
            yield return Wait(first.InitializeAsync(default, CancellationToken.None));
            first.BeginCombat(new HealthState(30, 30));
            var combat = first.Combat;
            EnqueueSpins(2);
            Assert.That(first.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            var accepted = host.PendingSlotAction;

            // The Scene is replaced while the spin is presented: controller and panel are destroyed, a new pair initializes.
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(panel.transform.root.gameObject);
            var secondPanel = CreatePanel(.0f);
            var second = CreateController(secondPanel);
            yield return Wait(second.InitializeAsync(default, CancellationToken.None));
            Assert.That(second.Combat, Is.SameAs(combat), "the new controller attaches to the session combat");
            Assert.That(second.CanExecute(TemporarySlotCombatController.SpinCommandId, out _), Is.False, "the accepted action still holds the lock");
            Assert.That(secondPanel.TotalText, Is.EqualTo(SlotCombatStoryFormatter.Total(accepted.Spin.PaylineTotal)), "the accepted spin is shown at rest");
            var before = story.MessageCount;
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);

            Assert.That(random.Calls, Is.EqualTo(3), "never spun again");
            Assert.That(combat.EnemyHealth.Current, Is.EqualTo(40 - accepted.Allocation.Lines[0].Amount), "applied once");
            Assert.That(story.MessageCount - before, Is.EqualTo(SlotCombatStoryFormatter.Turn(host.LastTemporarySlotTurn, "테스트 적").Count), "one Story record");
            Assert.That(second.CanExecute(TemporarySlotCombatController.SpinCommandId, out var reason), Is.True, reason);
        }

        [UnityTest]
        public IEnumerator PresentationWithPanel_EndsOnTheAcceptedResult()
        {
            yield return SetUpCombat();
            var panel = CreatePanel(0f);
            var controller = CreateController(panel);
            yield return Wait(controller.InitializeAsync(default, CancellationToken.None));
            controller.BeginCombat(new HealthState(30, 30));
            Assert.That(panel.TotalText, Is.EqualTo(SlotCombatStoryFormatter.IdleTotal));
            Assert.That(panel.AllocationText, Is.EqualTo("공격 ×1 · 회복 ×1"));
            random.Values.Enqueue(5); random.Values.Enqueue(0); random.Values.Enqueue(3);
            Assert.That(controller.TryExecute(TemporarySlotCombatController.SpinCommandId), Is.True);
            yield return WaitWhile(() => host.IsTemporaryCombatActionPending);
            Assert.That(panel.ReelViews.Select(v => v.PaylineValue), Is.EqualTo(new[] { 6, 1, 4 }));
            Assert.That(panel.TotalText, Is.EqualTo("합계 11"));
            Assert.That(panel.AllocationText, Is.EqualTo("공격 ×1 → 6 · 회복 ×1 → 5"));
        }

        // ---------- Reserved dice participants ----------

        [UnityTest]
        public IEnumerator DisabledDiceParticipants_TakeNoPart()
        {
            yield return SetUpCombat();
            var dice = Track(new GameObject("ReservedDice")).AddComponent<TemporaryDiceRollMenuController>();
            dice.ConfigureForEditor(null, story, combatConfiguration);
            dice.enabled = false;
            var binder = dice.gameObject.AddComponent<OwnedDiceSessionBinder>();
            binder.enabled = false;
            var diceInit = dice.InitializeAsync(default, CancellationToken.None);
            var binderInit = binder.InitializeAsync(default, CancellationToken.None);
            yield return Wait(diceInit);
            yield return Wait(binderInit);
            Assert.That(diceInit.IsFaulted || binderInit.IsFaulted, Is.False, "a disabled participant is not an error (an enabled binder without references would throw)");
            Assert.That(dice.IsReady, Is.False);
            var before = story.MessageCount;
            var record = new TemporaryDiceTurnRecord("run", "node", 1, Array.Empty<PlayerDieRoll>(),
                new TemporaryCombatTurnResult(new[] { new TemporaryCombatEvent(TemporaryCombatEventKind.EnemyAttack, 5, 5, 25, 30) }));
            host.PublishTemporaryDiceStory(record);
            host.QueueTemporaryDiceResult(record);
            Assert.That(story.MessageCount, Is.EqualTo(before), "no dice Story output");
            Assert.That(host.PendingDiceStory, Is.SameAs(record), "nobody consumed the dice story");
        }

        // ---------- Exploration reference ----------

        [Test]
        public void ExplorationCombatReference_KeepsTheFormerFieldNameAndRequiresTheInterface()
        {
            var field = typeof(ExplorationRunController).GetField("combatControllerBehaviour", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(field.GetCustomAttribute<FormerlySerializedAsAttribute>()?.oldName, Is.EqualTo("combatController"));
            var owner = Track(new GameObject("Exploration"));
            var exploration = owner.AddComponent<ExplorationRunController>();
            var notCombat = owner.AddComponent<ExplorationRunControllerProbe>();
            Assert.Throws<InvalidOperationException>(() => exploration.ConfigureCombatForEditor(notCombat));
            var slot = owner.AddComponent<TemporarySlotCombatController>();
            exploration.ConfigureCombatForEditor(slot);
            Assert.That(exploration.CombatControllerForEditor, Is.SameAs(slot));
            exploration.ConfigureCombatForEditor(owner.AddComponent<TemporaryDiceRollMenuController>());
        }

        [Test]
        public void SavedMainScene_UsesTheRenamedExplorationField()
        {
            var text = System.IO.File.ReadAllText(TemporarySlotMachineIntegration.ScenePath);
            Assert.That(text, Does.Contain("combatControllerBehaviour:"));
            Assert.That(text, Does.Not.Contain("  combatController:"));
        }

        [Test]
        public void SavedMainScene_IsWiredToTheSlotMachine()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(TemporarySlotMachineIntegration.ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(TemporarySlotMachineIntegration.ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try { Assert.DoesNotThrow(() => TemporarySlotMachineIntegration.ValidateScene(scene)); }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void StoryFormatter_MarksZeroLinesAndMatchesEventsBySource()
        {
            var catalog = new RuneDefinitionCatalog(new[]
            {
                new RuneDefinition("rune.attack", "공격 룬", RuneEffectKind.Attack),
                new RuneDefinition("rune.heal", "회복 룬", RuneEffectKind.Heal)
            });
            var inventory = new RuneInventory(catalog);
            inventory.TryAdd(new OwnedRune("a1", "rune.attack"), out _);
            inventory.TryAdd(new OwnedRune("h1", "rune.heal"), out _);
            var loadout = new RuneLoadout(inventory, new[] { new RuneLoadoutLine("line.1", new[] { "a1" }), new RuneLoadoutLine("line.2", new[] { "h1" }) });
            var allocation = WeightedRuneAllocationPolicy.Instance.Allocate(1, loadout, catalog);
            var actions = RuneCombatActions.FromAllocation(allocation);
            Assert.That(actions.Select(a => (a.Kind, a.Amount, a.SourceId)), Is.EqualTo(new[] { (PlayerCombatActionKind.Attack, 1, "line.1") }));
            var spin = new TxTRPG.Gameplay.SlotMachine.SlotMachine(new[] { SlotReel.FromValues("reel.1", new[] { 1 }) }).Spin(new SystemRandomIndexProvider(1));
            var combat = new TemporaryCombatState(30, 30, 40, 5, 3);
            var record = new TemporarySlotTurnRecord("run", "node", spin, allocation, combat.ExecuteTurn(actions));
            Assert.That(SlotCombatStoryFormatter.Turn(record, "슬라임"), Is.EqualTo(new[]
            {
                "[슬롯] 1 = 1",
                "[룬] 1번 줄 공격 룬 ×1: 1",
                "[전투] 슬라임에게 피해 1. 체력 39/40.",
                "[룬] 2번 줄 회복 룬 ×1: 0 (실행하지 않음)",
                "[적 행동] 공격 5. 플레이어가 받은 피해 5. 체력 25/30."
            }));
            Assert.Throws<InvalidOperationException>(() => RuneCombatActions.ToActionKind(RuneEffectKind.Unknown));
        }

        // ---------- Helpers ----------

        private IEnumerator SetUpCombat()
        {
            CreateHost(Track(ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>()),
                Track(ScriptableObject.CreateInstance<TemporaryRuneConfiguration>()));
            var initialization = host.EnsureInitializedAsync();
            yield return Wait(initialization);
            Assert.That(host.TemporarySlotMachine, Is.Not.Null, host.TemporarySlotCombatError);
            host.GetOrCreateTemporaryExploration(() => new ExplorationRunState("run", new HealthState(30, 30),
                new ExplorationNodeGenerator(2, new[] { new ExplorationNodeWeight(ExplorationNodeTypeIds.Combat, 1) }, new FixedExplorationRandom())));
        }

        private void CreateHost(TemporarySlotMachineConfiguration slots, TemporaryRuneConfiguration runes)
        {
            var profile = AssetDatabase.LoadAssetAtPath<NewGameProfile>(ApplicationProjectBuilder.DefaultNewGameProfilePath);
            var owner = Track(new GameObject("PlayerSessionHost"));
            host = owner.AddComponent<PlayerSessionHost>();
            host.Configure(profile, "slot-machine-tests.json", false);
            host.UseSessionForEditorTests(new PlayerSession(profile, new MemoryRepository(), new GuidCharacterInstanceIdGenerator()));
            host.ConfigureTemporarySlotCombat(slots, runes);
            PlayerSessionHost.SetInstanceForEditorTests(host);
            combatConfiguration = Track(ScriptableObject.CreateInstance<TemporaryCombatConfiguration>());
            random = new CountingRandom();
            var canvas = Track(new GameObject("StoryCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            story = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<StoryTextPanel>();
        }

        private TemporarySlotCombatController CreateController(SlotMachinePanel panel)
        {
            var controller = Track(new GameObject("SlotCombat")).AddComponent<TemporarySlotCombatController>();
            controller.ConfigureForEditor(null, null, story, combatConfiguration, null, null, null, null, panel);
            controller.SetRandomForTests(random);
            return controller;
        }

        // A panel whose presentation lasts `seconds` per reel. Time does not advance in Edit Mode, so a positive value
        // only ends through the controller timeout; zero finishes without waiting.
        private SlotMachinePanel CreatePanel(float seconds)
        {
            var canvas = Track(new GameObject("SlotCanvas", typeof(Canvas)));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run Apply Slot Machine Combat first.");
            var panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<SlotMachinePanel>();
            ((RectTransform)panel.transform).sizeDelta = new Vector2(360f, 280f);
            panel.SetTimingForEditor(seconds, 0f, 0f, 0f);
            return panel;
        }

        private void EnqueueSpins(int count)
        {
            for (var i = 0; i < count * 3; i++) random.Values.Enqueue(i % 6);
        }

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

        private sealed class CountingRandom : IRandomIndexProvider
        {
            public Queue<int> Values { get; } = new();
            public int Calls { get; private set; }
            public int NextIndex(int exclusiveUpperBound)
            {
                Calls++;
                return Values.Count > 0 ? Values.Dequeue() % exclusiveUpperBound : 0;
            }
        }

        private sealed class FixedExplorationRandom : IExplorationRandomSource
        {
            public int Next(int exclusiveMaximum) => 0;
        }

        private sealed class MemoryRepository : IPlayerSaveRepository
        {
            private PlayerSaveData saved;
            public Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken) => Task.FromResult(saved);
            public Task SaveAsync(PlayerSaveData data, CancellationToken cancellationToken) { saved = data; return Task.CompletedTask; }
        }
    }

    public sealed class ExplorationRunControllerProbe : MonoBehaviour { }
}
