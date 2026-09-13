using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class StoryTextPanelTests
    {
        [Test]
        public void CalculateOpacity_ReturnsFullOpacityBelowFadeStart()
        {
            var opacity = StoryTextPanel.CalculateOpacity(0.2f, 0.35f, 0.15f, 1.5f);
            Assert.That(opacity, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CalculateOpacity_ReturnsConfiguredMinimumAtTop()
        {
            var opacity = StoryTextPanel.CalculateOpacity(1f, 0.35f, 0.2f, 2f);
            Assert.That(opacity, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void CalculateInitialRevealProgress_StartsAtZero()
        {
            var progress = StoryTextPanel.CalculateInitialRevealProgress(0f, 20f);
            Assert.That(progress, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CalculateInitialRevealProgress_ReachesOneAtDuration()
        {
            var progress = StoryTextPanel.CalculateInitialRevealProgress(20f, 20f);
            Assert.That(progress, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void GeneratedPanelPrefab_HasRequiredReferences()
        {
            StoryTextPanelPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var panel = prefab.GetComponent<StoryTextPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.HasRequiredReferences, Is.True);
            Assert.That(prefab.GetComponent<ScrollRect>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<Scrollbar>(true), Is.Not.Null);
            Assert.That(prefab.GetComponent<Image>(), Is.Null);
            Assert.That(panel.BackgroundRenderer, Is.Not.Null);
            Assert.That(panel.BackgroundRenderer.InitialStyle, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(panel.BackgroundRenderer.InitialStyle),
                Is.EqualTo(StoryTextPanelPrefabBuilder.DefaultBackgroundStylePath));
            Assert.That(prefab.transform.Find(
                "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundA"), Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundB"), Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundEffectOverlay"), Is.Not.Null);
            Assert.That(prefab.transform.Find("ForegroundEffectLayer"), Is.Not.Null);
            Assert.That(prefab.transform.Find("TransitionOverlay"), Is.Not.Null);

            foreach (var path in new[]
                     {
                         "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundA",
                         "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundB",
                         "BackgroundLayer/BackgroundViewport/BackgroundVisualRoot/BackgroundEffectOverlay",
                         "ForegroundEffectLayer",
                         "TransitionOverlay"
                     })
            {
                Assert.That(prefab.transform.Find(path).GetComponent<Image>().raycastTarget, Is.False);
            }

            var messagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/StoryMessageItem.prefab");
            Assert.That(messagePrefab.transform.Find("Separator/Visual"), Is.Not.Null);
            Assert.That(
                messagePrefab.transform.Find("Separator/Visual").GetComponent<UnityEngine.UI.Image>(),
                Is.Not.Null);
        }

        [Test]
        public void Panel_CanClearAndResetDefaultBackground()
        {
            StoryTextPanelPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<StoryTextPanel>();
                var defaultStyle = panel.BackgroundRenderer.InitialStyle;
                panel.ClearBackground();
                Assert.That(panel.BackgroundRenderer.CurrentStyle, Is.Null);

                panel.ResetBackgroundToDefault();
                Assert.That(panel.BackgroundRenderer.CurrentStyle, Is.EqualTo(defaultStyle));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BackgroundRenderer_EffectMaterialCanRenderWithoutSpriteAndUseInstance()
        {
            var root = new GameObject("Background Renderer Test", typeof(RectTransform));
            var backgroundA = new GameObject("A", typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            var backgroundB = new GameObject("B", typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            var effect = new GameObject("Effect", typeof(RectTransform), typeof(Image))
                .GetComponent<Image>();
            backgroundA.transform.SetParent(root.transform, false);
            backgroundB.transform.SetParent(root.transform, false);
            effect.transform.SetParent(root.transform, false);
            var renderer = root.AddComponent<PanelBackgroundRenderer>();
            var style = ScriptableObject.CreateInstance<PanelBackgroundStyle>();
            var material = new Material(Shader.Find("UI/Default"));
            try
            {
                var rendererProperties = new SerializedObject(renderer);
                rendererProperties.FindProperty("backgroundA").objectReferenceValue = backgroundA;
                rendererProperties.FindProperty("backgroundB").objectReferenceValue = backgroundB;
                rendererProperties.FindProperty("effectOverlay").objectReferenceValue = effect;
                rendererProperties.ApplyModifiedPropertiesWithoutUndo();

                style.Configure(null, Color.black, FlexibleLayoutBackgroundScaleMode.Stretch);
                var styleProperties = new SerializedObject(style);
                styleProperties.FindProperty("effectMaterial").objectReferenceValue = material;
                styleProperties.FindProperty("effectTint").colorValue = Color.white;
                styleProperties.FindProperty("effectMaterialMode").enumValueIndex =
                    (int)FlexibleLayoutMaterialMode.Instance;
                styleProperties.ApplyModifiedPropertiesWithoutUndo();

                renderer.ApplyStyle(style);

                Assert.That(effect.gameObject.activeSelf, Is.True);
                Assert.That(effect.enabled, Is.True);
                Assert.That(effect.sprite, Is.Null);
                Assert.That(effect.material, Is.Not.Null);
                Assert.That(effect.material, Is.Not.SameAs(material));

                renderer.SetEffectsEnabled(false);
                Assert.That(effect.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(style);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void ActiveUnconfiguredPanel_ReportsMissingReferencesOnceWithoutException()
        {
            var gameObject = new GameObject("Unconfigured StoryTextPanel", typeof(RectTransform));
            gameObject.SetActive(false);
            try
            {
                var panel = gameObject.AddComponent<StoryTextPanel>();
                LogAssert.Expect(
                    LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        "StoryTextPanel is waiting for required references:.*scrollRect.*"));
                gameObject.SetActive(true);
                ((RectTransform)gameObject.transform).sizeDelta = new Vector2(420f, 260f);
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.HasRequiredReferences, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
