using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Runes;
using TxTRPG.UI.Runes;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class TemporaryRuneGridBinderTests
    {
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/Runes/RuneGridPanel.prefab";
        private GameObject canvas;
        private RuneGridPanel panel;
        private TemporaryRuneGridBinder binder;
        private TemporaryRuneConfiguration configuration;
        private TemporaryRuneState runes;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("RuneGridCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab: {PanelPrefabPath}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            panel = instance.GetComponent<RuneGridPanel>();
            binder = instance.AddComponent<TemporaryRuneGridBinder>();
            binder.Configure(panel);
            configuration = ScriptableObject.CreateInstance<TemporaryRuneConfiguration>();
            Assert.That(TemporaryRuneState.TryCreate(configuration, out runes, out var reason), Is.True, reason);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(configuration);
        }

        // Edit Mode does not call OnEnable/OnDisable for this component, so the tests invoke them like Unity would.
        private void Invoke(string method) =>
            typeof(TemporaryRuneGridBinder).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(binder, null);

        private string[] Badges() => panel.Cards.Take(panel.ActiveCardCount).Select(card => card.CountText.text).ToArray();

        [Test]
        public void CreateEntries_FollowsLineOrderWeightAndName()
        {
            Assert.That(runes.Inventory.TryAdd(new OwnedRune("rune.extra", TemporaryRuneConfiguration.HealRuneId), out _), Is.True);
            var healLine = runes.Loadout.Lines[1].LineId;
            Assert.That(runes.Loadout.TryAddRuneToLine(healLine, "rune.extra", out var reason), Is.True, reason);

            var entries = TemporaryRuneGridBinder.CreateEntries(runes);
            Assert.That(entries.Select(entry => entry.Key), Is.EqualTo(runes.Loadout.Lines.Select(line => line.LineId)));
            Assert.That(entries.Select(entry => entry.DefinitionId), Is.EqualTo(new[] { TemporaryRuneConfiguration.AttackRuneId, TemporaryRuneConfiguration.HealRuneId }));
            Assert.That(entries.Select(entry => entry.DisplayName), Is.EqualTo(new[] { "공격 룬", "회복 룬" }));
            Assert.That(entries.Select(entry => entry.StackCount), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(TemporaryRuneGridBinder.CreateEntries(null), Is.Empty);
        }

        [Test]
        public void CreateEntries_UnknownDefinition_ShowsTheUnknownName()
        {
            // The state's catalog has no definition for the line's rune.
            var unknownCatalog = new RuneDefinitionCatalog(new[] { new RuneDefinition("rune.attack", "공격 룬", RuneEffectKind.Attack) });
            var unknownInventory = new RuneInventory(unknownCatalog);
            unknownInventory.TryAdd(new OwnedRune("r1", "rune.attack"), out _);
            var loadout = new RuneLoadout(unknownInventory, new[] { new RuneLoadoutLine("line", new[] { "r1" }) });
            var otherCatalog = new RuneDefinitionCatalog(new[] { new RuneDefinition("rune.heal", "회복 룬", RuneEffectKind.Heal) });
            var unknown = TemporaryRuneGridBinder.CreateEntries(CreateState(otherCatalog, unknownInventory, loadout)).Single();
            Assert.That(unknown.DisplayName, Is.EqualTo(TemporaryRuneGridBinder.UnknownRuneName));
            Assert.That(unknown.DefinitionId, Is.Empty);
            Assert.That(unknown.StackCount, Is.EqualTo(1));
        }

        [Test]
        public void Connect_ShowsTheLinesAndRefreshesOnChanged()
        {
            binder.Connect(runes);
            Assert.That(panel.ActiveCardCount, Is.EqualTo(2));
            Assert.That(Badges(), Is.EqualTo(new[] { "1", "1" }));

            Assert.That(runes.Inventory.TryAdd(new OwnedRune("rune.extra", TemporaryRuneConfiguration.AttackRuneId), out _), Is.True);
            Assert.That(runes.Loadout.TryAddRuneToLine(runes.Loadout.Lines[0].LineId, "rune.extra", out var reason), Is.True, reason);
            Assert.That(Badges(), Is.EqualTo(new[] { "2", "1" }));

            Assert.That(runes.Loadout.TryReplaceAll(System.Array.Empty<RuneLoadoutLine>(), out reason), Is.True, reason);
            Assert.That(panel.IsShowingMessage, Is.True);
            Assert.That(panel.MessageText.text, Is.EqualTo(TemporaryRuneGridBinder.EmptyMessage));
        }

        [Test]
        public void MissingHostOrRuneState_ShowsTheUnavailableMessage()
        {
            binder.ConnectAsync(null).GetAwaiter().GetResult();
            Assert.That(panel.IsShowingMessage, Is.True);
            Assert.That(panel.MessageText.text, Is.EqualTo(TemporaryRuneGridBinder.UnavailableMessage));

            binder.Connect(runes);
            Assert.That(panel.IsShowingMessage, Is.False);
            binder.Connect(null);
            Assert.That(panel.MessageText.text, Is.EqualTo(TemporaryRuneGridBinder.UnavailableMessage));
        }

        [Test]
        public void Disable_UnsubscribesAndEnable_ResubscribesWithTheCurrentState()
        {
            binder.Connect(runes);
            Invoke("OnDisable");
            Assert.That(runes.Inventory.TryAdd(new OwnedRune("rune.extra", TemporaryRuneConfiguration.AttackRuneId), out _), Is.True);
            Assert.That(runes.Loadout.TryAddRuneToLine(runes.Loadout.Lines[0].LineId, "rune.extra", out _), Is.True);
            Assert.That(Badges(), Is.EqualTo(new[] { "1", "1" }), "a disabled binder does not refresh");

            Invoke("OnEnable");
            Assert.That(Badges(), Is.EqualTo(new[] { "2", "1" }), "enabling shows the current state");
            Assert.That(runes.Loadout.TryRemoveRuneFromLine(runes.Loadout.Lines[0].LineId, "rune.extra", out _), Is.True);
            Assert.That(Badges(), Is.EqualTo(new[] { "1", "1" }), "enabling subscribes again");

            Invoke("OnDestroy");
            Assert.That(runes.Loadout.TryAddRuneToLine(runes.Loadout.Lines[0].LineId, "rune.extra", out _), Is.True);
            Assert.That(Badges(), Is.EqualTo(new[] { "1", "1" }));
        }

        // TemporaryRuneState has only a configuration factory; build one around hand-made domain objects for edge cases.
        private static TemporaryRuneState CreateState(RuneDefinitionCatalog catalog, RuneInventory inventory, RuneLoadout loadout) =>
            (TemporaryRuneState)typeof(TemporaryRuneState)
                .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(RuneDefinitionCatalog), typeof(RuneInventory), typeof(RuneLoadout) }, null)
                .Invoke(new object[] { catalog, inventory, loadout });
    }
}
