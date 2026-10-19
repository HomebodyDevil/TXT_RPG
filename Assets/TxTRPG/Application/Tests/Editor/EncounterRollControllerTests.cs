using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TxTRPG.Application.Encounters;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.Encounters;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Tests
{
    // Uses the saved Prefabs read-only (instances live only in the test).
    public sealed class EncounterRollControllerTests
    {
        private const string Folder = "Assets/TxTRPG/UI/Prefabs/Encounters/";

        private GameObject canvas;
        private EncounterElementCatalog catalog;
        private EncounterRollConfiguration configuration;
        private EncounterElementToggleBar bar;
        private GameWindowService window;
        private MessageGameWindowPage page;
        private EncounterDebugPanel panel;
        private EncounterRollController controller;
        private readonly List<EncounterRollResult> completed = new();

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("EncounterControllerCanvas", typeof(Canvas));
            catalog = ScriptableObject.CreateInstance<EncounterElementCatalog>();
            catalog.SetEntries(new[] { new EncounterElementCatalog.Entry("rune", "룬") });
            configuration = ScriptableObject.CreateInstance<EncounterRollConfiguration>();
            configuration.SetValues(EncounterRollSettings.Default);
            bar = Instantiate<EncounterElementToggleBar>("EncounterElementToggleBar.prefab");
            window = Instantiate<GameWindowService>("EncounterResultModal.prefab");
            page = window.GetComponentInChildren<MessageGameWindowPage>(true);
            panel = Instantiate<EncounterDebugPanel>("EncounterDebugPanel.prefab");
            // Edit Mode does not run Awake; call it like Unity would so the host close events are wired.
            CallAwake(window.Host);
            CallAwake(window);
            controller = new GameObject("EncounterRollController").AddComponent<EncounterRollController>();
            controller.transform.SetParent(canvas.transform, false);
            controller.RollCompleted += completed.Add;
        }

        [TearDown]
        public void TearDown()
        {
            completed.Clear();
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(configuration);
        }

        private T Instantiate<T>(string file) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + file);
            Assert.That(prefab, Is.Not.Null, $"Missing {Folder + file}");
            return ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<T>();
        }

        private static void CallAwake(object target) =>
            target.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

        private void Setup(EncounterDebugPanel debugPanel, params int[] random)
        {
            controller.ConfigureForEditor(catalog, configuration, bar, window, page, debugPanel, new Scripted(random));
            Assert.That(controller.Initialize(), Is.True);
        }

        [Test]
        public void Occurred_OpensModalWithMessageAndRaisesRollCompleted()
        {
            Setup(panel, 0, 0, 0);
            bar.Views[0].Toggle.isOn = true; // user toggle
            Assert.That(controller.Selection.IsSelected("rune"), Is.True);

            var focus = panel.RollButton.gameObject;
            var result = controller.RollNow(focus);

            Assert.That(result.Outcome, Is.EqualTo(EncounterRollOutcome.Occurred));
            Assert.That(result.Kind, Is.EqualTo(EncounterKind.Combat));
            Assert.That(window.IsOpen, Is.True);
            Assert.That(window.CurrentPageId, Is.EqualTo(EncounterRollController.ResultPageId));
            Assert.That(window.LastRequest.ReturnFocus, Is.SameAs(focus));
            Assert.That(page.ConfiguredMessage, Is.EqualTo("룬 : 전투"));
            Assert.That(panel.ResultText.text, Is.EqualTo("룬 : 전투"));
            Assert.That(completed, Is.EqualTo(new[] { result }));
        }

        [Test]
        public void NotOccurred_DoesNotOpenModal()
        {
            Setup(panel, 9999);
            Assert.That(controller.RollNow().Outcome, Is.EqualTo(EncounterRollOutcome.NoElementsSelected));
            Assert.That(panel.ResultText.text, Is.EqualTo(EncounterResultFormatter.NoElementsMessage));
            controller.Selection.SetSelected("rune", true);
            Assert.That(bar.IsSelected("rune"), Is.True, "the bar shows the controller's selection");
            Assert.That(controller.RollNow().Outcome, Is.EqualTo(EncounterRollOutcome.ChanceFailed));
            Assert.That(panel.ResultText.text, Is.EqualTo(EncounterResultFormatter.ChanceFailedMessage));
            Assert.That(window.IsOpen, Is.False);
            Assert.That(completed.Count, Is.EqualTo(2));
        }

        [Test]
        public void OpenModal_RefusesToRoll()
        {
            Setup(panel, 0, 0, 0);
            controller.Selection.SetSelected("rune", true);
            Assert.That(controller.RollNow().Occurred, Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("result modal is open"));
            Assert.That(controller.RollNow(), Is.Null);
            Assert.That(completed.Count, Is.EqualTo(1));
            window.Host.RequestClose();
            Assert.That(window.IsOpen, Is.False, "closing through the host (outside click, Cancel, close button) closes the modal");
        }

        [Test]
        public void WorksWithoutDebugPanel()
        {
            Setup(null, 0, 0, 2999);
            controller.Selection.SetSelected("rune", true);
            var result = controller.RollNow();
            Assert.That(result.Kind, Is.EqualTo(EncounterKind.Rest));
            Assert.That(page.ConfiguredMessage, Is.EqualTo("룬 : 휴식"));
        }

        [Test]
        public void MissingRequiredReferences_StopWithoutException()
        {
            controller.ConfigureForEditor(null, configuration, bar, window, page, panel);
            LogAssert.Expect(LogType.Error, new Regex("EncounterRollController is missing"));
            Assert.That(controller.Initialize(), Is.False);
            Assert.That(controller.IsReady, Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("not ready"));
            Assert.That(controller.RollNow(), Is.Null);
            Assert.That(window.IsOpen, Is.False);
        }

        [Test]
        public void MissingModal_ReportsResultWithoutException()
        {
            LogAssert.Expect(LogType.Warning, new Regex("no result modal"));
            controller.ConfigureForEditor(catalog, configuration, bar, null, null, panel, new Scripted(0, 0, 1000));
            Assert.That(controller.Initialize(), Is.True);
            controller.Selection.SetSelected("rune", true);
            LogAssert.Expect(LogType.Warning, new Regex("룬 : 이벤트"));
            Assert.That(controller.RollNow().Kind, Is.EqualTo(EncounterKind.Event));
            Assert.That(panel.ResultText.text, Is.EqualTo("룬 : 이벤트"));
        }

        [Test]
        public void DebugPanel_DrivesSettingsResetAndRoll()
        {
            Setup(panel, 0, 0, 0);
            Assert.That(panel.OccurrenceValue.text, Is.EqualTo("50%"));
            panel.OccurrenceSlider.value = 100f;
            panel.CombatWeightSlider.value = 25f;
            Assert.That(controller.CurrentSettings.OccurrenceChance, Is.EqualTo(1f));
            Assert.That(controller.CurrentSettings.CombatWeight, Is.EqualTo(2.5f));
            Assert.That(configuration.CreateSettings().OccurrenceChance, Is.EqualTo(.5f), "the asset never changes");

            panel.ResetButton.onClick.Invoke();
            Assert.That(controller.CurrentSettings.OccurrenceChance, Is.EqualTo(.5f));
            Assert.That(panel.OccurrenceSlider.value, Is.EqualTo(50f));
            Assert.That(panel.CombatWeightValue.text, Is.EqualTo("1.0"));

            controller.Selection.SetSelected("rune", true);
            panel.RollButton.onClick.Invoke();
            Assert.That(completed.Count, Is.EqualTo(1));
            Assert.That(window.LastRequest.ReturnFocus, Is.SameAs(panel.RollButton.gameObject));
        }

        private sealed class Scripted : IRandomIndexProvider
        {
            private readonly Queue<int> values;
            public Scripted(params int[] values) => this.values = new Queue<int>(values ?? Array.Empty<int>());
            public int NextIndex(int exclusiveUpperBound) =>
                values.Count > 0 ? values.Dequeue() : throw new InvalidOperationException("Unexpected random call.");
        }
    }
}
