using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Encounters;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    // Reads the saved Prefab only.
    public sealed class EncounterElementToggleBarTests
    {
        private const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterElementToggleBar.prefab";
        private GameObject canvas;
        private EncounterElementToggleBar bar;
        private readonly List<(string id, bool isOn)> changes = new();

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("EncounterToggleCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing {PrefabPath}");
            bar = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<EncounterElementToggleBar>();
            bar.SelectionChanged += (id, isOn) => changes.Add((id, isOn));
        }

        [TearDown]
        public void TearDown()
        {
            changes.Clear();
            Object.DestroyImmediate(canvas);
        }

        private static EncounterElementToggleEntry[] Entries(params string[] ids) =>
            ids.Select(id => new EncounterElementToggleEntry(id, id.ToUpperInvariant())).ToArray();

        [Test]
        public void SavedPrefab_HasHorizontalOnlyScrollAndEmptyContent()
        {
            Assert.That(bar.IsValid(out var missing), Is.True, missing);
            Assert.That(bar.ScrollRect.horizontal, Is.True);
            Assert.That(bar.ScrollRect.vertical, Is.False);
            Assert.That(bar.Content.childCount, Is.EqualTo(0));
            Assert.That(bar.TogglePrefab.IsValid(out missing), Is.True, missing);
            var layout = bar.TogglePrefab.GetComponent<LayoutElement>();
            Assert.That(layout.minWidth, Is.GreaterThanOrEqualTo(96f));
            Assert.That(layout.minHeight, Is.GreaterThanOrEqualTo(48f));
            Assert.That(bar.TogglePrefab.Toggle.group, Is.Null, "multi-select: no ToggleGroup");
            var raycasts = bar.TogglePrefab.VisualRoot.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.raycastTarget).ToArray();
            Assert.That(raycasts, Is.Empty, "VisualRoot graphics are not input targets");
        }

        [Test]
        public void SetEntries_ReusesViewsAndDeactivatesExtras()
        {
            bar.SetEntries(Entries("a", "b", "c"));
            var first = bar.Views.ToArray();
            Assert.That(bar.ActiveCount, Is.EqualTo(3));
            bar.SetEntries(Entries("x"));
            Assert.That(bar.Views.Count, Is.EqualTo(3));
            Assert.That(bar.Views[0], Is.SameAs(first[0]));
            Assert.That(bar.Views.Select(view => view.gameObject.activeSelf), Is.EqualTo(new[] { true, false, false }));
            Assert.That(bar.Views[0].Label.text, Is.EqualTo("X"));
            bar.SetEntries(Entries("p", "q"));
            Assert.That(bar.Views.Count, Is.EqualTo(3));
            Assert.That(bar.Views.Select(view => view.gameObject.activeSelf), Is.EqualTo(new[] { true, true, false }));
        }

        [Test]
        public void Toggles_AllowMultipleSelectionAndReportChanges()
        {
            bar.SetEntries(Entries("a", "b"));
            bar.Views[0].Toggle.isOn = true;
            bar.Views[1].Toggle.isOn = true;
            Assert.That(bar.IsSelected("a") && bar.IsSelected("b"), Is.True);
            bar.Views[0].Toggle.isOn = false;
            Assert.That(changes, Is.EqualTo(new[] { ("a", true), ("b", true), ("a", false) }));
        }

        [Test]
        public void SetSelected_DoesNotNotify_AndShowsCheckmarkAndBorder()
        {
            bar.SetEntries(Entries("a"));
            var view = bar.Views[0];
            Assert.That(view.Checkmark.activeSelf || view.SelectedBorder.activeSelf, Is.False);
            bar.SetSelected("a", true);
            Assert.That(changes, Is.Empty);
            Assert.That(view.IsOn, Is.True);
            Assert.That(view.Checkmark.activeSelf && view.SelectedBorder.activeSelf, Is.True);
            bar.SetSelected("a", false);
            Assert.That(view.Checkmark.activeSelf || view.SelectedBorder.activeSelf, Is.False);
            view.Toggle.isOn = true;
            Assert.That(view.Checkmark.activeSelf && view.SelectedBorder.activeSelf, Is.True, "user toggles show the same marks");
        }

        [Test]
        public void Navigation_LinksNeighboursWithoutWrapping()
        {
            bar.SetEntries(Entries("a", "b", "c"));
            var toggles = bar.Views.Select(view => view.Toggle).ToArray();
            Assert.That(toggles.All(toggle => toggle.navigation.mode == Navigation.Mode.Explicit), Is.True);
            Assert.That(toggles[0].navigation.selectOnLeft, Is.Null);
            Assert.That(toggles[0].navigation.selectOnRight, Is.SameAs(toggles[1]));
            Assert.That(toggles[1].navigation.selectOnLeft, Is.SameAs(toggles[0]));
            Assert.That(toggles[1].navigation.selectOnRight, Is.SameAs(toggles[2]));
            Assert.That(toggles[2].navigation.selectOnRight, Is.Null);
            bar.SetEntries(Entries("a", "b"));
            Assert.That(toggles[1].navigation.selectOnRight, Is.Null, "the last active button does not lead to a hidden one");
        }
    }
}
