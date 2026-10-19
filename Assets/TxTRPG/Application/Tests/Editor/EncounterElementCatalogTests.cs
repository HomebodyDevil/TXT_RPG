using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Encounters;
using TxTRPG.UI.Encounters;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class EncounterElementCatalogTests
    {
        private const string CatalogPath = "Assets/TxTRPG/Application/Configuration/Encounters/EncounterElementCatalog.asset";
        private const string ConfigurationPath = "Assets/TxTRPG/Application/Configuration/Encounters/EncounterRollConfiguration.asset";
        private const string ToggleBarPrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterElementToggleBar.prefab";

        private EncounterElementCatalog catalog;
        private GameObject canvas;

        [TearDown]
        public void TearDown()
        {
            if (catalog != null) Object.DestroyImmediate(catalog);
            if (canvas != null) Object.DestroyImmediate(canvas);
        }

        [Test]
        public void Validate_ReportsEmptyDuplicateAndNameless()
        {
            catalog = ScriptableObject.CreateInstance<EncounterElementCatalog>();
            catalog.SetEntries(new[]
            {
                new EncounterElementCatalog.Entry("rune", "룬"),
                new EncounterElementCatalog.Entry(" ", "빈 ID"),
                new EncounterElementCatalog.Entry("rune", "중복"),
                new EncounterElementCatalog.Entry("relic", "  "),
                null
            });
            Assert.That(catalog.Validate(out var problems), Is.False);
            Assert.That(problems.Count, Is.EqualTo(4));
            Assert.That(problems.Any(problem => problem.Contains("empty id")), Is.True);
            Assert.That(problems.Any(problem => problem.Contains("duplicate id 'rune'")), Is.True);
            Assert.That(problems.Any(problem => problem.Contains("empty display name")), Is.True);
            var entries = catalog.CreateToggleEntries();
            Assert.That(entries.Select(entry => entry.Id), Is.EqualTo(new[] { "rune" }), "invalid entries get no button");
            Assert.That(entries[0].DisplayName, Is.EqualTo("룬"));
        }

        [Test]
        public void SavedCatalog_HasOnlyRuneAndIsValid()
        {
            var saved = AssetDatabase.LoadAssetAtPath<EncounterElementCatalog>(CatalogPath);
            Assert.That(saved, Is.Not.Null, $"Missing {CatalogPath}");
            Assert.That(saved.Validate(out var problems), Is.True, string.Join("; ", problems));
            Assert.That(saved.Entries.Select(entry => entry.Id), Is.EqualTo(new[] { "rune" }));
            Assert.That(saved.Entries.Select(entry => entry.DisplayName), Is.EqualTo(new[] { "룬" }));
        }

        [Test]
        public void SavedConfiguration_HasDefaultValues()
        {
            var saved = AssetDatabase.LoadAssetAtPath<EncounterRollConfiguration>(ConfigurationPath);
            Assert.That(saved, Is.Not.Null, $"Missing {ConfigurationPath}");
            var settings = saved.CreateSettings();
            Assert.That(settings.OccurrenceChance, Is.EqualTo(.5f));
            Assert.That(settings.ElementInclusionChance, Is.EqualTo(.5f));
            Assert.That(new[] { settings.CombatWeight, settings.EventWeight, settings.RestWeight }, Is.EqualTo(new[] { 1f, 1f, 1f }));
        }

        [Test]
        public void AddingAnEntry_AddsAButton()
        {
            catalog = ScriptableObject.CreateInstance<EncounterElementCatalog>();
            catalog.SetEntries(new[] { new EncounterElementCatalog.Entry("rune", "룬") });
            canvas = new GameObject("EncounterCatalogCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToggleBarPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing {ToggleBarPrefabPath}");
            var bar = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<EncounterElementToggleBar>();

            bar.SetEntries(catalog.CreateToggleEntries());
            Assert.That(bar.ActiveCount, Is.EqualTo(1));

            catalog.SetEntries(catalog.Entries.Concat(new[] { new EncounterElementCatalog.Entry("relic", "유물") }));
            bar.SetEntries(catalog.CreateToggleEntries());
            Assert.That(bar.ActiveCount, Is.EqualTo(2));
            Assert.That(bar.Views.Take(2).Select(view => view.Label.text), Is.EqualTo(new[] { "룬", "유물" }));
        }
    }
}
