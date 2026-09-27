using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Dice;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class TemporaryMenuIconTests
    {
        [Test]
        public void CustomSprite_SurvivesPrefabSaveReloadAndRepeatedMigration()
        {
            var path = $"Assets/TxTRPG/Application/Tests/Editor/__MenuIconRoundTrip_{System.Guid.NewGuid():N}.prefab";
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath));
            try
            {
                var view = root.GetComponent<GameMenuPanel>().Buttons.Single(b => b.PageId == "system").View;
                view.SetIcon(TemporaryMenuIconUtility.GetDefault("action"));
                view.ConfigureDisplay(GameMenuButtonDisplayMode.ImageWithLabel, 13f, Color.cyan);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var menu = loaded.GetComponent<GameMenuPanel>();
                    Assert.That(TemporaryMenuIconUtility.ApplyToMenu(menu), Is.False);
                    Assert.That(TemporaryMenuIconUtility.ApplyToMenu(menu), Is.False);
                    var saved = menu.Buttons.Single(b => b.PageId == "system").View;
                    Assert.That(saved.Icon.sprite, Is.SameAs(TemporaryMenuIconUtility.GetDefault("action")));
                    Assert.That(saved.Icon.color, Is.EqualTo(Color.cyan));
                    Assert.That(saved.DisplayMode, Is.EqualTo(GameMenuButtonDisplayMode.ImageWithLabel));
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }
            finally { Object.DestroyImmediate(root); AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void SavedAssets_HaveDistinctSpritesAndPreservedBindings()
        {
            var sprites = TemporaryMenuIconUtility.Keys.Select(TemporaryMenuIconUtility.GetDefault).ToArray();
            Assert.That(sprites.Distinct().Count(), Is.EqualTo(4));
            foreach (var key in TemporaryMenuIconUtility.Keys)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(TemporaryMenuIconUtility.PathFor(key));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.mipmapEnabled, Is.False);
            }
            foreach (var path in new[] { QuickItemsUiProjectBuilder.GameMenuPrefabPath, QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { AssertMenu(root.GetComponentInChildren<GameMenuPanel>(true), false); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(QuickItemsUiProjectBuilder.ScenePath, OpenSceneMode.Additive);
            try { AssertMenu(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenuPanel>(true)).Single(), true); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertMenu(GameMenuPanel menu, bool action)
        {
            foreach (var key in TemporaryMenuIconUtility.Keys.Where(k => action || k != "action"))
            {
                var binding = key == "action"
                    ? menu.Buttons.Single(b => b.ActionKind == GameMenuButtonActionKind.Command && b.CommandId == TemporaryDiceRollMenuController.RollAllCommandId)
                    : menu.Buttons.Single(b => b.PageId == key);
                var view = binding.View;
                Assert.That(view.Icon.sprite, Is.SameAs(TemporaryMenuIconUtility.GetDefault(key)));
                Assert.That(view.DisplayMode, Is.EqualTo(GameMenuButtonDisplayMode.ImageOnly));
                Assert.That(view.Icon.gameObject.activeSelf, Is.True);
                Assert.That(view.Label.gameObject.activeSelf, Is.False);
                Assert.That(view.Label.text, Is.EqualTo(key == "system" ? "System" : key == "inventory" ? "Bag" : key == "status" ? "Status" : "행동"));
                Assert.That(view.Icon.raycastTarget, Is.False);
            }
        }

        [Test]
        public void ReplacementFallbackAndSelectiveMigration_PreserveCustomDisplayAndGeometry()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(QuickItemsUiProjectBuilder.GameMenuPrefabPath));
            try
            {
                var view = root.GetComponentsInChildren<GameMenuButtonView>(true).First();
                var rect = (RectTransform)view.transform;
                var size = rect.sizeDelta;
                var custom = TemporaryMenuIconUtility.GetDefault("action");
                view.SetIcon(custom);
                view.ConfigureDisplay(GameMenuButtonDisplayMode.ImageOnly, 11f, Color.cyan);
                Assert.That(TemporaryMenuIconUtility.ApplyDefault(view, "system"), Is.False);
                Assert.That(view.Icon.sprite, Is.SameAs(custom));
                Assert.That(view.Icon.color, Is.EqualTo(Color.cyan));
                view.SetIcon(custom, false);
                Assert.That(view.Label.gameObject.activeSelf, Is.True);
                Assert.That(view.Icon.gameObject.activeSelf, Is.False);
                view.SetIcon(null);
                Assert.That(view.Label.gameObject.activeSelf, Is.True);
                view.Icon.sprite = custom;
                view.RefreshDisplay();
                Assert.That(view.Label.gameObject.activeSelf, Is.False);
                view.Icon.enabled = false; view.RefreshDisplay();
                Assert.That(view.Label.gameObject.activeSelf, Is.True);
                view.SetIcon(custom);
                view.gameObject.SetActive(false); view.gameObject.SetActive(true);
                Assert.That(view.Icon.gameObject.activeSelf, Is.True);
                Assert.That(view.Icon.preserveAspect, Is.True);
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
                var serialized = new SerializedObject(view);
                serialized.FindProperty("icon").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo(); view.RefreshDisplay();
                Assert.That(view.Label.gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
