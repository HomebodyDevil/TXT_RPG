using System.Collections;
using NUnit.Framework;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class EditorValidationLayoutTests
    {
        [UnityTest]
        public IEnumerator HealthBar_SerializedChangesApplyAfterValidationAndReenable()
        {
            var root = new GameObject("HealthBarTest", typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(200f, 80f);
                var bar = new GameObject("BarRoot", typeof(RectTransform)).GetComponent<RectTransform>();
                bar.SetParent(rootRect, false);
                var controller = root.AddComponent<HealthBarLayoutController>();
                controller.Configure(rootRect, bar, HealthBarHorizontalAlignment.Center,
                    HealthBarVerticalAlignment.Middle, HealthBarAxisSizeMode.Fixed,
                    HealthBarAxisSizeMode.Fixed, new Vector2(40f, 20f), new RectOffset(), Vector2.zero);

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("fixedSize").vector2Value = new Vector2(70f, 30f);
                serialized.ApplyModifiedProperties();
                yield return null;
                Assert.That(bar.rect.size, Is.EqualTo(new Vector2(70f, 30f)));

                controller.enabled = false;
                serialized.Update();
                serialized.FindProperty("fixedSize").vector2Value = new Vector2(90f, 35f);
                serialized.ApplyModifiedProperties();
                yield return null;
                Assert.That(bar.rect.size, Is.EqualTo(new Vector2(70f, 30f)));
                controller.enabled = true;
                yield return null;
                Assert.That(bar.rect.size, Is.EqualTo(new Vector2(90f, 35f)));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator Scrollbar_SerializedWidthChangesApplyOnEditorUpdate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponentInChildren<ConfigurableScrollbarController>(true);
                Assert.That(controller, Is.Not.Null);
                controller.Visibility = ScrollbarVisibilityMode.Always;
                controller.SpaceMode = ScrollbarSpaceMode.ReserveAlways;
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("width").floatValue = 27f;
                serialized.ApplyModifiedProperties();
                yield return null;
                Assert.That(controller.ReservedInset, Is.EqualTo(27f + controller.Gap).Within(0.01f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [UnityTest]
        public IEnumerator GameMenu_ZeroViewportWaitsThenRecalculatesAndReportsRealShortfall()
        {
            var root = new GameObject("MenuTest", typeof(RectTransform));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = Vector2.zero;
                var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect))
                    .GetComponent<RectTransform>();
                viewport.SetParent(rootRect, false);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.sizeDelta = Vector2.zero;
                var content = new GameObject("Content", typeof(RectTransform), typeof(GameMenuLayoutGroup))
                    .GetComponent<RectTransform>();
                content.SetParent(viewport, false);
                var button = new GameObject("Button", typeof(RectTransform)).GetComponent<RectTransform>();
                button.SetParent(content, false);
                var scrollbar = new GameObject("Scrollbar", typeof(RectTransform), typeof(Scrollbar))
                    .GetComponent<Scrollbar>();
                scrollbar.transform.SetParent(rootRect, false);
                var scrollRect = viewport.GetComponent<ScrollRect>();
                scrollRect.viewport = viewport;
                scrollRect.content = content;
                var layout = content.GetComponent<GameMenuLayoutGroup>();
                layout.SetViewport(viewport);
                var panel = root.AddComponent<GameMenuPanel>();
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("viewport").objectReferenceValue = viewport;
                serialized.FindProperty("content").objectReferenceValue = content;
                serialized.FindProperty("scrollRect").objectReferenceValue = scrollRect;
                serialized.FindProperty("layout").objectReferenceValue = layout;
                serialized.FindProperty("horizontalScrollbar").objectReferenceValue = scrollbar;
                serialized.ApplyModifiedProperties();
                yield return null;
                Assert.That(panel.HasValidLayout, Is.False);
                Assert.That(panel.LayoutInsufficientSpace, Is.False);
                Assert.That(panel.LayoutReadinessReason, Does.Contain("Viewport has no usable size"));

                rootRect.sizeDelta = new Vector2(500f, 100f);
                panel.ConfigureScrollbar(GameMenuScrollbarVisibility.Hidden,
                    GameMenuScrollbarSpaceMode.Overlay, 12f, 4f);
                yield return null;
                Assert.That(panel.HasValidLayout, Is.True);
                Assert.That(panel.LayoutInsufficientSpace, Is.False);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "GameMenuPanel has insufficient layout space"));
                rootRect.sizeDelta = new Vector2(10f, 10f);
                panel.RefreshLayout();
                yield return null;
                Assert.That(panel.LayoutInsufficientSpace, Is.True);

                rootRect.sizeDelta = new Vector2(500f, 100f);
                panel.RefreshLayout();
                yield return null;
                Assert.That(panel.LayoutInsufficientSpace, Is.False);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                    "GameMenuPanel has insufficient layout space"));
                rootRect.sizeDelta = new Vector2(10f, 10f);
                panel.RefreshLayout();
                yield return null;
                Assert.That(panel.LayoutInsufficientSpace, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
