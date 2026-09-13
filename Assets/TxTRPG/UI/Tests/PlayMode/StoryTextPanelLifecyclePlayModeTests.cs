using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class StoryTextPanelLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator MissingReferences_ResizeThenConfigure_RecoversAndCleansUp()
        {
            var root = new GameObject("StoryTextPanel Lifecycle Test", typeof(RectTransform));
            root.SetActive(false);
            var panel = root.AddComponent<StoryTextPanel>();
            try
            {
                LogAssert.Expect(LogType.Warning,
                    new System.Text.RegularExpressions.Regex(
                        "StoryTextPanel is waiting for required references:.*scrollRect.*"));
                root.SetActive(true);
                ((RectTransform)root.transform).sizeDelta = new Vector2(400f, 240f);
                ((RectTransform)root.transform).sizeDelta = new Vector2(420f, 260f);
                yield return null;

                Configure(panel, root.transform);
                yield return null;
                yield return null;

                Assert.That(panel.HasRequiredReferences, Is.True);
                Assert.That(Get<bool>(panel, "scrollListenersRegistered"), Is.True);
                Assert.That(Get<bool>(panel, "layoutRefreshInProgress"), Is.False);

                root.SetActive(false);
                Assert.That(Get<bool>(panel, "scrollListenersRegistered"), Is.False);
                Assert.That(Get<object>(panel, "scrollToBottomRoutine"), Is.Null);
                Assert.That(Get<object>(panel, "initialRevealRoutine"), Is.Null);

                root.SetActive(true);
                yield return null;
                Assert.That(Get<bool>(panel, "scrollListenersRegistered"), Is.True);
            }
            finally
            {
                Object.Destroy(root);
            }

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ConfiguredPanel_RepeatedResizeAndCanvasUpdates_DoesNotReenterOrThrow()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = new GameObject("StoryTextPanel Resize Test", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(canvasObject.transform, false);
            var panel = root.AddComponent<StoryTextPanel>();
            Configure(panel, root.transform);
            root.SetActive(true);
            try
            {
                for (var i = 0; i < 8; i++)
                {
                    ((RectTransform)root.transform).sizeDelta =
                        new Vector2(360f + i * 17f, 220f + i * 9f);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Assert.That(Get<bool>(panel, "layoutRefreshInProgress"), Is.False);
                }

                root.SetActive(false);
                yield return null;
                Object.Destroy(root);
                yield return null;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (root != null) Object.Destroy(root);
                Object.Destroy(canvasObject);
            }
        }

        private static void Configure(StoryTextPanel panel, Transform root)
        {
            var viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(root, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;

            var contentObject = new GameObject(
                "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport, false);
            var content = (RectTransform)contentObject.transform;
            var scrollRect = root.GetComponent<ScrollRect>() ?? root.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var scrollbarObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(root, false);
            var messagePrefabObject = new GameObject("StoryMessageItem Test Prefab", typeof(RectTransform));
            messagePrefabObject.SetActive(false);
            messagePrefabObject.transform.SetParent(root, false);
            var messagePrefab = messagePrefabObject.AddComponent<StoryMessageItem>();

            Set(panel, "messagePrefab", messagePrefab);
            Set(panel, "viewport", viewport);
            Set(panel, "content", content);
            Set(panel, "scrollRect", scrollRect);
            Set(panel, "scrollbar", scrollbarObject.GetComponent<Scrollbar>());
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private static T Get<T>(object target, string field) =>
            (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
    }
}
