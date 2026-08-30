using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class CharacterDisplayPanelTests
    {
        [Test]
        public void Presentation_NormalizesNullIdentifiers()
        {
            var presentation = new CharacterPresentation(null, null, null, null, null);

            Assert.That(presentation.CharacterId, Is.Empty);
            Assert.That(presentation.AppearanceId, Is.Empty);
            Assert.That(presentation.PoseId, Is.Empty);
            Assert.That(presentation.ExpressionId, Is.Empty);
            Assert.That(presentation.AnimationId, Is.Empty);
        }

        [Test]
        public void GeneratedPrefab_HasPanelAndReplaceable2DView()
        {
            CharacterDisplayPanelPrefabBuilder.CreateOrUpdatePrefab();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterDisplayPanel>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("DisplayRoot/Character2DView"), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<Character2DView>(true), Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "DisplayRoot/Character2DView/FrameViewport/VisualRoot/ArtworkRoot/BaseImage")
                .GetComponent<Image>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("DisplayRoot/Character2DView/FrameViewport")
                .GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionOverlay"), Is.Not.Null);
        }

        [Test]
        public void GeneratedDemo_HasVisiblePreviewAndConnectedLoader()
        {
            CharacterDisplayPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<CharacterDisplayPanelDemoData>(
                "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemoData.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab");

            Assert.That(data, Is.Not.Null);
            Assert.That(data.AppearanceDefinition, Is.Not.Null);
            Assert.That(data.AppearanceDefinition.TryResolveReference(
                data.ToPresentation(), out var artwork), Is.True);
            Assert.That(artwork.AssetId, Is.Not.Empty);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterDisplayPanelDemoLoader>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<CharacterDisplayPanel>(true), Is.Not.Null);

            var baseImage = prefab.transform
                .Find("CharacterDisplayPanel/DisplayRoot/Character2DView/FrameViewport/VisualRoot/ArtworkRoot/BaseImage")
                .GetComponent<Image>();
            Assert.That(baseImage.enabled, Is.True);
            Assert.That(baseImage.sprite, Is.Not.Null);
        }
    }
}
