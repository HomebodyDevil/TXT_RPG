using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Exploration;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.Gameplay.Exploration.Events;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class ExplorationEventAssetTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        private ExplorationEventDefinition Definition(string id)
        {
            var definition = Track(ScriptableObject.CreateInstance<ExplorationEventDefinition>());
            ExplorationTestEventContent.Configure(definition, id);
            return definition;
        }

        [Test]
        public void Definition_BecomesAValidatedModel()
        {
            var definition = Definition("event.test.slot-trial");
            Assert.That(definition.TryCreateEvent(out var model, out var reason), Is.True, reason);
            Assert.That(model.EventId, Is.EqualTo("event.test.slot-trial"));
            Assert.That(model.Pages.Count, Is.EqualTo(4));
            var check = model.GetPage("start").Interaction as ExplorationEventSlotCheck;
            Assert.That(check, Is.Not.Null);
            Assert.That(check.UseFortuneCritical, Is.True);
            Assert.That(check.Outcomes.Select(o => (o.Minimum, o.Maximum)), Is.EqualTo(new (int?, int?)[] { (null, 6), (7, 12), (13, null) }));
            Assert.That(definition.CollectWarnings(), Is.Empty);
        }

        [Test]
        public void Definition_ReportsValidationErrors()
        {
            var definition = Track(ScriptableObject.CreateInstance<ExplorationEventDefinition>());
            definition.ConfigureForEditor("event.bad", "Bad", "", "start", new[]
            {
                new ExplorationEventPageDefinition("start", "", "", new ExplorationEventContinueDefinition("go", new ExplorationEventBranchDefinition("nowhere")))
            });
            Assert.That(definition.TryCreateEvent(out _, out var reason), Is.False);
            Assert.That(reason, Does.Contain("nowhere"));
            definition.ConfigureForEditor("event.bad", "Bad", "", "start", new[]
            {
                new ExplorationEventPageDefinition("start", "", "", new ExplorationEventContinueDefinition("go", new ExplorationEventBranchDefinition(ExplorationEventTargets.End, new ChangeHealthEffect(0))))
            });
            Assert.That(definition.TryCreateEvent(out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("cannot be 0"));
            definition.ConfigureForEditor("event.bad", "Bad", "", "start", new[] { new ExplorationEventPageDefinition("start", "", "", null) });
            Assert.That(definition.TryCreateEvent(out _, out _), Is.False, "no interaction");
            definition.ConfigureForEditor("", "Bad", "", "start", new[] { new ExplorationEventPageDefinition() });
            Assert.That(definition.TryCreateEvent(out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("event ID"));
        }

        [Test]
        public void Catalog_SkipsInvalidEventsAndRejectsStructuralErrors()
        {
            var good = Definition("event.test.signpost");
            var broken = Track(ScriptableObject.CreateInstance<ExplorationEventDefinition>());
            broken.ConfigureForEditor("event.broken", "Broken", "", "missing", new[] { new ExplorationEventPageDefinition() });
            var catalog = Track(ScriptableObject.CreateInstance<ExplorationEventCatalog>());
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(good, 2), new ExplorationEventCatalogEntry(broken, 1) });
            var skipped = new List<string>();
            Assert.That(catalog.TryBuild(out var runtime, out var error, skipped), Is.True, error);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(runtime.TryGet("event.test.signpost", out _), Is.True);
            Assert.That(skipped.Single(), Does.Contain("event.broken"));

            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(good, 1), new ExplorationEventCatalogEntry(good, 1) });
            Assert.That(catalog.TryBuild(out _, out error), Is.False);
            Assert.That(error, Does.Contain("duplicated"));
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(good, -1) });
            Assert.That(catalog.TryBuild(out _, out _), Is.False, "negative weight");
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(null, 1) });
            Assert.That(catalog.TryBuild(out _, out _), Is.False, "empty entry");
            catalog.ConfigureForEditor(new ExplorationEventCatalogEntry[0]);
            Assert.That(catalog.TryBuild(out _, out _), Is.False, "empty catalog");
            catalog.ConfigureForEditor(new[] { new ExplorationEventCatalogEntry(good, 0) });
            Assert.That(catalog.TryBuild(out runtime, out error), Is.True, error);
            Assert.That(runtime.CanPick, Is.False, "all weights 0: no event node can appear");
        }

        [Test]
        public void Configuration_WithoutEventsTreatsTheEventWeightAsZero()
        {
            var configuration = Track(ScriptableObject.CreateInstance<ExplorationRunConfiguration>());
            Assert.That(configuration.NodeWeights.Select(w => (w.TypeId, w.Weight)), Is.EqualTo(new[]
                { (ExplorationNodeTypeIds.Combat, 40), (ExplorationNodeTypeIds.RecoveryUpgrade, 30), (ExplorationNodeTypeIds.Event, 30) }));
            Assert.That(configuration.CreateWeights(false).Single(w => w.TypeId == ExplorationNodeTypeIds.Event).Weight, Is.Zero);
            Assert.That(configuration.CreateWeights(true).Single(w => w.TypeId == ExplorationNodeTypeIds.Event).Weight, Is.EqualTo(30));
            configuration.ConfigureForEditor(3, new[] { new ExplorationNodeWeightEntry("combat", 1), new ExplorationNodeWeightEntry("combat", 1) });
            Assert.That(configuration.Validate(out var reason), Is.False);
            Assert.That(reason, Does.Contain("twice"));
        }

        [Test]
        public void Configuration_MigratesTheOldFixedWeightsUnchanged()
        {
            var configuration = Track(ScriptableObject.CreateInstance<ExplorationRunConfiguration>());
            // The serialized form of the old asset (choiceCount, combatWeight, recoveryUpgradeWeight).
            EditorJsonUtility.FromJsonOverwrite("{\"MonoBehaviour\":{\"choiceCount\":4,\"combatWeight\":60,\"recoveryUpgradeWeight\":15}}", configuration);
            Assert.That(configuration.ChoiceCount, Is.EqualTo(4));
            Assert.That(configuration.NodeWeights.Select(w => (w.TypeId, w.Weight)), Is.EqualTo(new[]
                { (ExplorationNodeTypeIds.Combat, 60), (ExplorationNodeTypeIds.RecoveryUpgrade, 15) }));
            Assert.That(configuration.EventWeight, Is.Zero, "the migration does not add the new event weight");
            var json = EditorJsonUtility.ToJson(configuration);
            Assert.That(json, Does.Contain("\"combatWeight\":-1"), "the old fields are marked as migrated");
            var reloaded = Track(ScriptableObject.CreateInstance<ExplorationRunConfiguration>());
            EditorJsonUtility.FromJsonOverwrite(json, reloaded);
            Assert.That(reloaded.CombatWeight, Is.EqualTo(60));
            Assert.That(reloaded.RecoveryUpgradeWeight, Is.EqualTo(15));
        }

        [Test]
        public void TestEventContent_IsValidForAllFive()
        {
            foreach (var (id, _, _) in TemporaryExplorationEventNodesApply.TestEvents)
            {
                var definition = Definition(id);
                Assert.That(definition.TryCreateEvent(out _, out var reason), Is.True, $"{id}: {reason}");
                Assert.That(definition.CollectWarnings(), Is.Empty, id);
            }
        }

        // The saved assets (created by Apply Exploration Event Nodes) pass validation and are in the catalog.
        [Test]
        public void SavedTestEventAssets_PassValidation()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ExplorationEventCatalog>(TemporaryExplorationEventNodesApply.CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Run Tools/TxT RPG/Application/Temporary/Apply Exploration Event Nodes first.");
            var skipped = new List<string>();
            Assert.That(catalog.TryBuild(out var runtime, out var error, skipped), Is.True, error);
            Assert.That(skipped, Is.Empty);
            foreach (var (id, file, _) in TemporaryExplorationEventNodesApply.TestEvents)
            {
                var definition = AssetDatabase.LoadAssetAtPath<ExplorationEventDefinition>($"{TemporaryExplorationEventNodesApply.TestEventsFolder}/{file}.asset");
                Assert.That(definition, Is.Not.Null, file);
                Assert.That(definition.TryCreateEvent(out _, out var reason), Is.True, $"{id}: {reason}");
                Assert.That(runtime.TryGet(id, out _), Is.True, id);
            }
        }
    }
}
