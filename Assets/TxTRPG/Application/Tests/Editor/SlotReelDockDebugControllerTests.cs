using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TxTRPG.Application.Dice;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.SlotMachine;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace TxTRPG.Application.Tests
{
    // Reads the saved SlotReelDock.prefab and EncounterDebugPanel.prefab only; the configuration assets are test instances.
    public sealed class SlotReelDockDebugControllerTests
    {
        private const string DockPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotReelDock.prefab";
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab";
        private GameObject canvas;
        private SlotReelDock dock;
        private SlotDebugTabView tab;
        private SlotReelDockDebugController controller;
        private TemporarySlotMachineConfiguration slotConfiguration;
        private TemporaryFortuneCriticalConfiguration fortuneConfiguration;
        private TemporarySlotMachineState slots;
        private TemporaryFortuneCriticalState fortune;
        private TemporaryDiceResultPreferences preferences;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("DockDebugCanvas", typeof(Canvas));
            var overlay = new GameObject("OverlayLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            overlay.SetParent(canvas.transform, false);
            overlay.sizeDelta = new Vector2(1920f, 1080f);
            dock = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DockPrefabPath), overlay)).GetComponent<SlotReelDock>();
            dock.SetRespectSafeAreaForEditor(false);
            dock.SetSpinSettingsForEditor(SlotReelSpinSettings.Instant);
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath), overlay);
            tab = panel.GetComponentInChildren<SlotDebugTabView>(true);
            panel.GetComponent<TxTRPG.UI.DebugPanels.DebugPanelTabGroup>().Select(1);

            slotConfiguration = ScriptableObject.CreateInstance<TemporarySlotMachineConfiguration>();
            slotConfiguration.ConfigureForEditor(new[]
            {
                new TemporarySlotReelDefinition("reel.1", 1, 2, 3),
                new TemporarySlotReelDefinition("reel.2", 4, 5, 6)
            });
            fortuneConfiguration = ScriptableObject.CreateInstance<TemporaryFortuneCriticalConfiguration>();
            fortuneConfiguration.ConfigureForEditor(new TemporaryFortuneCriticalRuleDefinition(.05f), null, new TemporaryFortuneCriticalRuleDefinition(.03f), 30);
            Assert.That(TemporarySlotMachineState.TryCreate(slotConfiguration, out slots, out var reason), Is.True, reason);
            fortune = TemporaryFortuneCriticalState.Create(fortuneConfiguration, FortuneWeightFunctionRegistry.Default, out var configured, out reason);
            Assert.That(configured, Is.True, reason);
            preferences = new TemporaryDiceResultPreferences { ReduceMotion = true };

            controller = canvas.AddComponent<SlotReelDockDebugController>();
            controller.ConfigureForEditor(dock, tab, slotConfiguration, fortuneConfiguration, 10);
            controller.SetRandomForTests(new FixedRandom(0));
            // The largest draw never triggers a chance below 100%.
            controller.SetFortuneRandomForTests(new FixedRandom(int.MaxValue));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(slotConfiguration);
            Object.DestroyImmediate(fortuneConfiguration);
        }

        private void Connect() => controller.Connect(slots, fortune, preferences);

        private static FortuneCriticalRule Always(int multiplier)
        {
            Assert.That(FortuneCriticalRule.TryCreate(1d, multiplier, multiplier,
                FortuneMultiplierDistribution.Function(FortuneWeightFunctionRegistry.DefaultFunctionId, FortuneFunctionArguments.Create(System.Array.Empty<System.Collections.Generic.KeyValuePair<string, double>>())),
                FortuneWeightFunctionRegistry.Default, out var rule, out var reason), Is.True, reason);
            return rule;
        }

        [Test]
        public void Connect_ShowsTheSessionReelsOnTheDockAndTab()
        {
            Connect();
            Assert.That(controller.IsConnected, Is.True);
            Assert.That(dock.ReelCount, Is.EqualTo(2));
            Assert.That(dock.ReelViews.Select(view => view.ReelId), Is.EqualTo(new[] { "reel.1", "reel.2" }));
            Assert.That(dock.TotalValueText, Is.EqualTo(SlotCombatStoryFormatter.IdleTotalValue));
            Assert.That(dock.ReduceMotion, Is.True);
            Assert.That(tab.ActiveRowCount, Is.EqualTo(2));
            Assert.That(tab.GetRow(0).Value.text, Is.EqualTo("5%"));
            Assert.That(tab.TotalValue.text, Is.EqualTo("3%"));
            Assert.That(tab.IsSessionAvailable, Is.True);
        }

        [Test]
        public void AddReel_UsesTheSmallestFreeIdAndCopiesTheLastStrip()
        {
            Connect();
            Assert.That(slots.Machine.TryRemoveReel("reel.1", out var reason), Is.True, reason);
            controller.ChangeReelCount(1);
            Assert.That(slots.Machine.Reels.Select(reel => reel.Id), Is.EqualTo(new[] { "reel.2", "reel.1" }));
            Assert.That(slots.Machine.Reels[1].Strip.Select(symbol => symbol.Value), Is.EqualTo(new[] { 4, 5, 6 }), "copy of the last reel");
            controller.ChangeReelCount(1);
            Assert.That(slots.Machine.Reels[2].Id, Is.EqualTo("reel.3"));
            Assert.That(dock.ReelCount, Is.EqualTo(3), "the dock follows the session");
            Assert.That(tab.ActiveRowCount, Is.EqualTo(3));
            Assert.That(tab.GetRow(2).Label.text, Is.EqualTo("Reel 3"));
        }

        [Test]
        public void RemoveReel_RemovesItsOwnRule_AndKeepsTheLimits()
        {
            Connect();
            controller.SetReelChance("reel.2", .5f);
            Assert.That(fortune.Rules.TryGetReelRule("reel.2", out _), Is.True);
            controller.ChangeReelCount(-1);
            Assert.That(slots.ReelCount, Is.EqualTo(1));
            Assert.That(fortune.Rules.TryGetReelRule("reel.2", out _), Is.False, "the removed reel's rule is removed");
            controller.ChangeReelCount(-1);
            Assert.That(slots.ReelCount, Is.EqualTo(1), "one reel always stays");
            Assert.That(tab.MinusButton.interactable, Is.False);

            for (var i = 0; i < 12; i++) controller.ChangeReelCount(1);
            Assert.That(slots.ReelCount, Is.EqualTo(10));
            Assert.That(dock.ReelCount, Is.EqualTo(10));
            Assert.That(tab.PlusButton.interactable, Is.False);
            Assert.That(fortune.Rules.RuleForReel("reel.2").Chance, Is.EqualTo(fortune.Rules.DefaultReelRule.Chance), "added again: default rule");
        }

        [Test]
        public void ChanceChanges_GoToTheSessionRules()
        {
            Connect();
            var changed = 0;
            fortune.Changed += () => changed++;
            tab.GetRow(0).Slider.value = 40f;
            tab.TotalSlider.value = 70f;
            Assert.That(fortune.Rules.RuleForReel("reel.1").Chance, Is.EqualTo(.4d).Within(1e-6));
            Assert.That(fortune.Rules.RuleForReel("reel.2").Chance, Is.EqualTo(.05d).Within(1e-6));
            Assert.That(fortune.Rules.TotalRule.Chance, Is.EqualTo(.7d).Within(1e-6));
            Assert.That(changed, Is.EqualTo(2));
            Assert.That(slotConfiguration.Reels.Count, Is.EqualTo(2), "the configuration asset is not changed");
            Assert.That(fortuneConfiguration.DefaultReelRule.Chance, Is.EqualTo(.05f));
        }

        [Test]
        public void Spin_ResolvesFortuneCriticalAndShowsTheResult()
        {
            Connect();
            Assert.That(fortune.TrySetReelRule("reel.1", Always(2), out var reason), Is.True, reason);
            Assert.That(fortune.TrySetTotalRule(Always(3), out reason), Is.True, reason);
            controller.SetRandomForTests(new FixedRandom(2, 1));
            var task = controller.SpinAsync();
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(controller.IsSpinning, Is.False);
            // reel.1 stops on 3 (×2), reel.2 on 5; total ×3: 3×min(30, 6) + 5×3 = 33.
            Assert.That(controller.LastResultMessage, Is.EqualTo("3×2, 5 → 합계 ×3 = 33"));
            Assert.That(tab.ResultText.text, Is.EqualTo(controller.LastResultMessage));
            Assert.That(dock.TotalValueText, Is.EqualTo("33"));
            Assert.That(dock.TotalMultiplierText, Is.EqualTo("×3"));
            Assert.That(tab.SpinButton.interactable, Is.True, "the spin limits are released");
        }

        [Test]
        public void FormatResult_AddsTheCapNote()
        {
            Assert.That(FortuneCriticalRules.TryCreate(Always(10), System.Array.Empty<FortuneCriticalReelRule>(), Always(5), 30, out var rules, out var reason), Is.True, reason);
            Assert.That(slots.Machine.TryReplaceAll(new[] { SlotReel.FromValues("reel.1", new[] { 2 }) }, out reason), Is.True, reason);
            var result = FortuneCriticalResolver.Resolve(slots.Spin(new FixedRandom(0)), rules, new FixedRandom(0));
            Assert.That(SlotReelDockDebugController.FormatResult(result), Is.EqualTo("2×10 → 합계 ×5 = 60 (상한 ×30 적용)"));
        }

        [Test]
        public void Spin_Overflow_IsReportedWithoutPlaying()
        {
            Connect();
            Assert.That(slots.Machine.TryReplaceAll(new[] { SlotReel.FromValues("reel.1", new[] { 1_500_000_000 }) }, out var reason), Is.True, reason);
            Assert.That(fortune.TrySetReelRule("reel.1", Always(2), out reason), Is.True, reason);
            var task = controller.SpinAsync();
            Assert.That(task.IsCompleted && !task.IsFaulted, Is.True);
            Assert.That(controller.LastResultMessage, Does.StartWith("합계가 지원 범위를 넘었습니다"));
            Assert.That(dock.TotalValueText, Is.EqualTo(SlotCombatStoryFormatter.IdleTotalValue), "nothing was played");
            Assert.That(controller.IsSpinning, Is.False);
        }

        [Test]
        public void Reset_RestoresTheConfigurationValues_AndKeepsTheShownState()
        {
            Connect();
            controller.ChangeReelCount(1);
            controller.SetReelChance("reel.1", 1f);
            controller.SetTotalChance(0f);
            controller.HideDock();
            controller.ResetToDefaults();
            Assert.That(slots.Machine.Reels.Select(reel => reel.Id), Is.EqualTo(new[] { "reel.1", "reel.2" }));
            Assert.That(fortune.Rules.TryGetReelRule("reel.1", out _), Is.False);
            Assert.That(fortune.Rules.TotalRule.Chance, Is.EqualTo(.03d).Within(1e-6));
            Assert.That(controller.LastResultMessage, Is.EqualTo(SlotReelDockDebugController.ResetDoneMessage));
            Assert.That(dock.IsShown, Is.False);
            Assert.That(tab.ShowButton.interactable, Is.True);
            Assert.That(tab.HideButton.interactable, Is.False);
        }

        [Test]
        public void ShowAndHide_UseTheDock()
        {
            Connect();
            tab.HideButton.onClick.Invoke();
            Assert.That(dock.IsShown, Is.False);
            Assert.That(tab.HideButton.interactable, Is.False);
            tab.ShowButton.onClick.Invoke();
            Assert.That(dock.IsShown, Is.True);
            Assert.That(tab.ShowButton.interactable, Is.False);
        }

        [Test]
        public void WithoutSession_OnlyMovesWork()
        {
            controller.Connect(null, null, null);
            Assert.That(controller.IsConnected, Is.False);
            Assert.That(tab.ResultText.text, Is.EqualTo(SlotReelDockDebugController.NoSessionMessage));
            Assert.That(tab.SpinButton.interactable || tab.PlusButton.interactable || tab.ResetButton.interactable, Is.False);
            controller.ChangeReelCount(1);
            Assert.That(controller.SpinAsync().IsCompleted, Is.True);
            tab.HideButton.onClick.Invoke();
            Assert.That(dock.IsShown, Is.False);
        }

        [Test]
        public void MissingDock_ReportsOnceAndDoesNothing()
        {
            controller.ConfigureForEditor(null, tab, slotConfiguration, fortuneConfiguration, 10);
            LogAssert.Expect(LogType.Error, new Regex("no SlotReelDock"));
            Connect();
            Connect();
            Assert.That(controller.IsConnected, Is.False);
        }

        private sealed class FixedRandom : IRandomIndexProvider
        {
            private readonly int[] values;
            private int index;
            public FixedRandom(params int[] sequence) => values = sequence;
            public int NextIndex(int exclusiveUpperBound) => System.Math.Min(values[index++ % values.Length], exclusiveUpperBound - 1);
        }
    }
}
