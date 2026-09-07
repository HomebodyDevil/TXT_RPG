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
            Assert.That(presentation.VisualStateId, Is.Empty);
        }

        [Test]
        public void Presentation_WithVisualStatePreservesExistingSelectionAxes()
        {
            var presentation = new CharacterPresentation(
                "character.hero",
                "armor.blue",
                "critical",
                "battle",
                "focused",
                "idle",
                true);

            Assert.That(presentation.VisualStateId, Is.EqualTo("critical"));
            Assert.That(presentation.PoseId, Is.EqualTo("battle"));
            Assert.That(presentation.Mirrored, Is.True);
        }

        [Test]
        public void GeneratedPrefab_HasPanelAndReplaceable2DView()
        {
            CharacterDisplayPanelPrefabBuilder.CreateOrUpdatePrefab();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterDisplayPanel>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterDisplayPanel>().BackgroundRenderer, Is.Not.Null);
            Assert.That(prefab.transform.Find("BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundA"), Is.Not.Null);
            Assert.That(prefab.transform.Find("BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundB"), Is.Not.Null);
            Assert.That(prefab.transform.Find("BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundEffectOverlay"), Is.Not.Null);
            Assert.That(prefab.transform.Find("DisplayRoot/Character2DView"), Is.Not.Null);
            var characterView = prefab.GetComponentInChildren<Character2DView>(true);
            Assert.That(characterView, Is.Not.Null);
            Assert.That(new SerializedObject(characterView)
                .FindProperty("animateVisibility").boolValue, Is.False);
            Assert.That(prefab.transform.Find(
                "DisplayRoot/Character2DView/FrameViewport/VisualRoot/ArtworkRoot/BaseImage")
                .GetComponent<Image>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("DisplayRoot/Character2DView/FrameViewport")
                .GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionOverlay"), Is.Not.Null);
            Assert.That(prefab.transform.Find("ForegroundEffectLayer"), Is.Not.Null);
            foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                Assert.That(image.raycastTarget, Is.False, image.name);
        }

        [Test]
        public void GeneratedStatusPrefabs_UseComposableNonInteractiveHealthBar()
        {
            CharacterStatusPrefabBuilder.CreateOrUpdatePrefabs();

            var healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterStatusPrefabBuilder.HealthBarPrefabPath);
            var statusPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CharacterStatusPrefabBuilder.StatusPanelPrefabPath);
            Assert.That(healthPrefab, Is.Not.Null);
            Assert.That(statusPrefab, Is.Not.Null);
            Assert.That(
                healthPrefab.transform.Find("BarRoot/BarVisualRoot/Slider/Fill Area/Fill/FillVisualRoot/FillImage"),
                Is.Not.Null);
            Assert.That(
                healthPrefab.transform.Find("BarRoot/BarVisualRoot/BarEffectOverlay"),
                Is.Not.Null);
            Assert.That(healthPrefab.transform.Find("TextLayer/TextVisualRoot/LabelText"), Is.Not.Null);
            Assert.That(healthPrefab.transform.Find("TextLayer/TextVisualRoot/ValueText"), Is.Not.Null);
            Assert.That(healthPrefab.transform.Find("ForegroundEffectLayer"), Is.Not.Null);
            Assert.That(healthPrefab.transform.Find("TransitionOverlay"), Is.Not.Null);

            var slider = healthPrefab.GetComponentInChildren<Slider>(true);
            Assert.That(slider, Is.Not.Null);
            Assert.That(slider.interactable, Is.False);
            Assert.That(slider.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(slider.handleRect, Is.Null);
            Assert.That(slider.wholeNumbers, Is.True);

            var instance = Object.Instantiate(statusPrefab);
            try
            {
                var panel = instance.GetComponent<CharacterStatusPanel>();
                var healthBar = instance.GetComponentInChildren<HealthBarPanel>(true);
                Assert.That(panel, Is.Not.Null);
                Assert.That(healthBar, Is.Not.Null);
                Assert.That(panel.Elements, Has.Count.EqualTo(1));

                var name = new CharacterNamePresentation(string.Empty);
                var health = new HealthPresentation(37, 100, "HP", "37 / 100");
                panel.Apply(new CharacterStatusPresentation(name, health));
                Assert.That(healthBar.Slider.minValue, Is.Zero);
                Assert.That(healthBar.Slider.maxValue, Is.EqualTo(100));
                Assert.That(healthBar.Slider.value, Is.EqualTo(37));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HealthBarPanel_AllowsEveryOptionalVisualReferenceToBeMissing()
        {
            var instance = new GameObject("HealthBar", typeof(HealthBarPanel));
            try
            {
                var panel = instance.GetComponent<HealthBarPanel>();
                var name = new CharacterNamePresentation(string.Empty);
                var health = new HealthPresentation(1, 2, string.Empty, string.Empty);
                Assert.DoesNotThrow(() =>
                    panel.Apply(new CharacterStatusPresentation(name, health)));
                Assert.DoesNotThrow(panel.Clear);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
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
            Assert.That(prefab.GetComponent<PanelStartupController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<FadePanelRevealTransition>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<CharacterDisplayPanel>(true), Is.Not.Null);

            var baseImage = prefab.transform
                .Find("CharacterDisplayPanel/DisplayRoot/Character2DView/FrameViewport/VisualRoot/ArtworkRoot/BaseImage")
                .GetComponent<Image>();
            Assert.That(baseImage.enabled, Is.True);
            Assert.That(baseImage.sprite, Is.Not.Null);
            var backgroundStyle = AssetDatabase.LoadAssetAtPath<PanelBackgroundStyle>(
                "Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayBackgroundDemoStyle.asset");
            Assert.That(backgroundStyle, Is.Not.Null);
            var renderer = prefab.GetComponentInChildren<CharacterDisplayPanel>(true).BackgroundRenderer;
            var rendererProperties = new SerializedObject(renderer);
            Assert.That(rendererProperties.FindProperty("initialStyle").objectReferenceValue,
                Is.EqualTo(backgroundStyle));
        }
    }
}
