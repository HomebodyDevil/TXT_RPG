using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.DebugPanels;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class DebugPanelTabGroupTests
    {
        private GameObject root;
        private DebugPanelTabGroup group;
        private readonly List<DebugPanelTab> tabs = new();
        private readonly List<int> changes = new();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Panel", typeof(RectTransform));
            for (var i = 0; i < 2; i++)
            {
                var button = new GameObject($"Tab{i}", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                button.transform.SetParent(root.transform, false);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                label.transform.SetParent(button.transform, false);
                var indicator = new GameObject("SelectedIndicator", typeof(RectTransform));
                indicator.transform.SetParent(button.transform, false);
                var page = new GameObject($"Page{i}", typeof(RectTransform));
                page.transform.SetParent(root.transform, false);
                var first = new GameObject("First", typeof(RectTransform), typeof(Image), typeof(Slider)).GetComponent<Slider>();
                first.transform.SetParent(page.transform, false);
                tabs.Add(new DebugPanelTab(button, label, indicator, page, first));
            }
            group = root.AddComponent<DebugPanelTabGroup>();
            group.ConfigureForEditor(tabs, 0);
            group.SelectionChanged += changes.Add;
        }

        [TearDown]
        public void TearDown()
        {
            tabs.Clear();
            changes.Clear();
            Object.DestroyImmediate(root);
        }

        [Test]
        public void StartsOnTheInitialTab_WithOnlyItsPageActive()
        {
            Assert.That(group.IsValid(out var missing), Is.True, missing);
            Assert.That(group.SelectedIndex, Is.Zero);
            Assert.That(tabs[0].Page.activeSelf, Is.True);
            Assert.That(tabs[1].Page.activeSelf, Is.False);
            Assert.That(changes, Is.Empty, "the initial selection raises nothing");
        }

        [Test]
        public void Select_SwitchesPages_AndNotifiesOnce()
        {
            Assert.That(group.SelectedIndex, Is.Zero);
            tabs[1].Button.onClick.Invoke();
            Assert.That(group.SelectedIndex, Is.EqualTo(1));
            Assert.That(tabs[0].Page.activeSelf, Is.False);
            Assert.That(tabs[1].Page.activeSelf, Is.True);
            Assert.That(changes, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void SameTabOrOutOfRange_IsIgnored()
        {
            group.Select(0);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("tab index 5"));
            group.Select(5);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("tab index -1"));
            group.Select(-1);
            Assert.That(group.SelectedIndex, Is.Zero);
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void Selection_IsShownByIndicatorBoldAndNavigation()
        {
            group.Select(1);
            Assert.That(tabs[1].SelectedIndicator.activeSelf, Is.True);
            Assert.That(tabs[0].SelectedIndicator.activeSelf, Is.False);
            Assert.That((tabs[1].Label.fontStyle & FontStyles.Bold) != 0, Is.True);
            Assert.That((tabs[0].Label.fontStyle & FontStyles.Bold) != 0, Is.False);
            var navigation = tabs[0].Button.navigation;
            Assert.That(navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(navigation.selectOnRight, Is.EqualTo(tabs[1].Button));
            Assert.That(navigation.selectOnDown, Is.EqualTo(tabs[1].FirstControl), "down goes to the selected page");
            Assert.That(tabs[1].Button.navigation.selectOnLeft, Is.EqualTo(tabs[0].Button));
        }
    }
}
