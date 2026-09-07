using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TxTRPG.Content.Characters.Editor;
using TxTRPG.Editor.Common.Menu;
using TxTRPG.SceneTransition.Editor;
using TxTRPG.UI.Editor;
using UnityEditor;

namespace TxTRPG.Editor.Tests
{
    public sealed class EditorMenuTests
    {
        [Test]
        public void TxTRPGMenus_UseExpectedHierarchyAndPriorities()
        {
            var expected = new Dictionary<string, int>
            {
                [TxTRPGEditorMenuPaths.CharacterContent + "Open Authoring"] =
                    TxTRPGEditorMenuPriorities.Authoring,
                [TxTRPGEditorMenuPaths.CharacterContent +
                 "Create Default Placeholder Content"] =
                    TxTRPGEditorMenuPriorities.Create,
                [TxTRPGEditorMenuPaths.CharacterContent + "Validate All"] =
                    TxTRPGEditorMenuPriorities.Validate,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Story Text Panel"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Character Display Panel"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Enemy Display Panel"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Action Grid"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Rebuild Flexible Layout"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Upgrade Selected Flexible Layouts"] =
                    TxTRPGEditorMenuPriorities.Refresh,
                [TxTRPGEditorMenuPaths.UiPrefabs + "Upgrade Selected Health Bars"] =
                    TxTRPGEditorMenuPriorities.Refresh,
                [TxTRPGEditorMenuPaths.UiPrefabs +
                 "Convert Selected Health Bars to Padding Sizing"] =
                    TxTRPGEditorMenuPriorities.Refresh + 1,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Story Text Panel Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos +
                 "Rebuild Character Display Panel Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Health Bar Panel Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Enemy Display Panel Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Action Grid Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Flexible Layout Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiDemos + "Rebuild Sample Main Layout Demo"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.UiPreview + "Refresh Story Text Panel"] =
                    TxTRPGEditorMenuPriorities.Refresh,
                [TxTRPGEditorMenuPaths.Application + "Rebuild App Scene"] =
                    TxTRPGEditorMenuPriorities.Rebuild,
                [TxTRPGEditorMenuPaths.Application +
                 "Validate App Scene Configuration"] =
                    TxTRPGEditorMenuPriorities.Validate,
                [TxTRPGEditorMenuPaths.Addressables + "Register UI Assets"] =
                    TxTRPGEditorMenuPriorities.Register,
                [TxTRPGEditorMenuPaths.Addressables + "Validate Settings"] =
                    TxTRPGEditorMenuPriorities.Validate,
                [TxTRPGEditorMenuPaths.Addressables + "Build Player Content"] =
                    TxTRPGEditorMenuPriorities.Build
            };

            var assemblies = new[]
            {
                typeof(CharacterAuthoringWindow).Assembly,
                typeof(AddressableAssetEditor).Assembly,
                typeof(SceneTransitionPrefabBuilder).Assembly
            }.Distinct();
            var actual = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static))
                .Select(method => method.GetCustomAttribute<MenuItem>())
                .Where(attribute => attribute != null &&
                    attribute.menuItem.StartsWith(
                        TxTRPGEditorMenuPaths.Root,
                        StringComparison.Ordinal))
                .ToArray();

            Assert.That(actual.Length, Is.EqualTo(expected.Count));
            Assert.That(
                actual.Select(attribute => attribute.menuItem),
                Is.EquivalentTo(expected.Keys));
            foreach (var attribute in actual)
            {
                Assert.That(
                    attribute.priority,
                    Is.EqualTo(expected[attribute.menuItem]),
                    attribute.menuItem);
            }
            Assert.That(
                actual.Select(attribute => attribute.menuItem).Distinct().Count(),
                Is.EqualTo(actual.Length),
                "TxT RPG menu paths must be unique.");
        }
    }
}
