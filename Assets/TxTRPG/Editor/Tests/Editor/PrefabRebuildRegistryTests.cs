using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Editor.Common.PrefabRebuild;

namespace TxTRPG.Editor.Tests
{
    public sealed class PrefabRebuildRegistryTests
    {
        private static PrefabRebuildTaskDescriptor Task(string id, string[] outputs = null,
            string[] dependencies = null) => new(id, id, PrefabRebuildCategory.Production, true,
                Array.Empty<string>(), outputs ?? new[] { $"Assets/TxTRPG/UI/Prefabs/{id}.prefab" },
                dependencies ?? Array.Empty<string>(), string.Empty, () => { });

        [Test]
        public void DiscoveredRegistry_IsValidAndContainsOnlyExplicitSafeTasks()
        {
            var registry = PrefabRebuildRegistry.Discover();
            Assert.That(registry.ValidateRegistry(), Is.Empty);
            Assert.That(registry.Tasks.Select(x => x.Id), Is.EquivalentTo(new[]
            {
                "ui.action-grid",
                "ui.character-display",
                "ui.enemy-display",
                "ui.exploration-node-choice-card",
                "ui.flexible-layout"
            }));
            Assert.That(registry.Exclusions, Is.Not.Empty);
        }

        [Test]
        public void ValidationRejectsDuplicateIdsAndOutputOwners()
        {
            var registry = PrefabRebuildRegistry.CreateForValidation(new[]
            { Task("same"), Task("same"), Task("other", new[] { "Assets/TxTRPG/UI/Prefabs/same.prefab" }) });
            var errors = registry.ValidateRegistry();
            Assert.That(errors.Any(x => x.Contains("Duplicate")), Is.True);
            Assert.That(errors.Any(x => x.Contains("multiple owners")), Is.True);
        }

        [Test]
        public void PlanIsStableIncludesDependenciesOnceAndRejectsCycles()
        {
            var registry = PrefabRebuildRegistry.CreateForValidation(new[]
            { Task("a"), Task("b", dependencies: new[] { "a" }), Task("c", dependencies: new[] { "a" }) });
            Assert.That(registry.BuildPlan(new[] { "c", "b", "a" }).Select(x => x.Id), Is.EqualTo(new[] { "a", "b", "c" }));
            var cyclic = PrefabRebuildRegistry.CreateForValidation(new[]
            { Task("x", dependencies: new[] { "y" }), Task("y", dependencies: new[] { "x" }) });
            Assert.Throws<InvalidOperationException>(() => cyclic.BuildPlan(new[] { "x" }));
        }

        [Test]
        public void ValidationRejectsPathsOutsideDeclaredProjectArea()
        {
            var registry = PrefabRebuildRegistry.CreateForValidation(new[]
            { Task("bad", new[] { "Assets/../ProjectSettings.asset" }) });
            Assert.That(registry.ValidateRegistry().Any(x => x.Contains("invalid path")), Is.True);
        }
    }
}
