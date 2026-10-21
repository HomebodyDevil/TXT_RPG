using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.DebugPanels;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    // Reads the saved Prefab only.
    public sealed class EncounterDebugPanelTests
    {
        private const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab";
        private GameObject canvas;
        private EncounterDebugPanel panel;
        private readonly List<EncounterDebugValues> changes = new();
        private int rolls;
        private int resets;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("EncounterDebugCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing {PrefabPath}");
            panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<EncounterDebugPanel>();
            panel.ValuesChanged += changes.Add;
            panel.RollRequested += () => rolls++;
            panel.ResetRequested += () => resets++;
        }

        [TearDown]
        public void TearDown()
        {
            changes.Clear();
            rolls = resets = 0;
            Object.DestroyImmediate(canvas);
        }

        [Test]
        public void SavedPrefab_IsCompleteAndDevelopmentOnly()
        {
            Assert.That(panel.IsValid(out var missing), Is.True, missing);
            Assert.That(panel.GetComponent<DevelopmentBuildOnly>(), Is.Not.Null);
            Assert.That(panel.IsExpanded, Is.True, "expanded by default");
        }

        [Test]
        public void SetValues_DoesNotNotify_AndFormatsValues()
        {
            panel.SetValues(new EncounterDebugValues(.5f, .25f, 1f, 0f, 10f));
            Assert.That(changes, Is.Empty);
            Assert.That(panel.OccurrenceValue.text, Is.EqualTo("50%"));
            Assert.That(panel.InclusionValue.text, Is.EqualTo("25%"));
            Assert.That(panel.CombatWeightValue.text, Is.EqualTo("1.0"));
            Assert.That(panel.EventWeightValue.text, Is.EqualTo("0.0"));
            Assert.That(panel.RestWeightValue.text, Is.EqualTo("10.0"));
            Assert.That(panel.CurrentValues, Is.EqualTo(new EncounterDebugValues(.5f, .25f, 1f, 0f, 10f)));
        }

        [Test]
        public void SliderChanges_RaiseValuesChanged()
        {
            panel.SetValues(new EncounterDebugValues(.5f, .5f, 1f, 1f, 1f));
            panel.OccurrenceSlider.value = 73f;
            panel.RestWeightSlider.value = 35f;
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[1].OccurrenceChance, Is.EqualTo(.73f).Within(1e-5f));
            Assert.That(changes[1].RestWeight, Is.EqualTo(3.5f).Within(1e-5f));
            Assert.That(panel.OccurrenceValue.text, Is.EqualTo("73%"));
            Assert.That(panel.RestWeightValue.text, Is.EqualTo("3.5"));
            Assert.That(panel.OccurrenceSlider.wholeNumbers && panel.OccurrenceSlider.maxValue == 100f, Is.True, "1% steps");
            Assert.That(panel.RestWeightSlider.wholeNumbers && panel.RestWeightSlider.maxValue == 100f, Is.True, "0.1 steps up to 10");
        }

        [Test]
        public void Buttons_RaiseEvents_AndResultIsShown()
        {
            panel.SetValues(new EncounterDebugValues(.5f, .5f, 1f, 1f, 1f));
            panel.RollButton.onClick.Invoke();
            panel.ResetButton.onClick.Invoke();
            Assert.That(rolls, Is.EqualTo(1));
            Assert.That(resets, Is.EqualTo(1));
            panel.ShowResult("룬 : 전투");
            Assert.That(panel.ResultText.text, Is.EqualTo("룬 : 전투"));
        }

        [Test]
        public void CollapseButton_TogglesBody()
        {
            panel.SetExpanded(true);
            panel.CollapseButton.onClick.Invoke();
            Assert.That(panel.IsExpanded, Is.False);
            Assert.That(panel.Body.activeSelf, Is.False);
            panel.CollapseButton.onClick.Invoke();
            Assert.That(panel.IsExpanded, Is.True);
        }
            [Test]
        public void SavedPrefab_HasEncounterAndSlotTabs()
        {
            var tabs = panel.GetComponent<DebugPanelTabGroup>();
            Assert.That(tabs, Is.Not.Null);
            Assert.That(tabs.IsValid(out var missing), Is.True, missing);
            Assert.That(tabs.TabCount, Is.EqualTo(2));
            Assert.That(tabs.Tabs[0].Label.text, Is.EqualTo("이벤트 판정"));
            Assert.That(tabs.Tabs[1].Label.text, Is.EqualTo("슬롯"));
            Assert.That(tabs.SelectedIndex, Is.Zero, "the encounter tab is selected first");
            Assert.That(tabs.Tabs[0].Page.activeSelf, Is.True);
            Assert.That(tabs.Tabs[1].Page.activeSelf, Is.False);
            Assert.That(tabs.Tabs[1].Page.GetComponent<SlotDebugTabView>(), Is.Not.Null);
            foreach (var tab in tabs.Tabs)
            {
                var rect = (RectTransform)tab.Button.transform;
                Assert.That(UnityEngine.UI.LayoutUtility.GetMinHeight(rect), Is.GreaterThanOrEqualTo(48f), rect.name);
                Assert.That(UnityEngine.UI.LayoutUtility.GetMinWidth(rect), Is.GreaterThanOrEqualTo(96f), rect.name);
                Assert.That(tab.Page.transform.IsChildOf(panel.Body.transform), Is.True, "collapsing hides the pages");
                Assert.That(tab.Button.transform.IsChildOf(panel.Body.transform), Is.True, "collapsing hides the tab bar");
            }
            Assert.That(panel.OccurrenceSlider.transform.IsChildOf(tabs.Tabs[0].Page.transform), Is.True, "the encounter controls moved to its page");
            Assert.That(panel.ResultText.transform.IsChildOf(tabs.Tabs[0].Page.transform), Is.True);
            Assert.That(panel.transform.Find("Header/Title").GetComponent<TMP_Text>().text, Is.EqualTo("DEBUG"));
        }
    }
}
