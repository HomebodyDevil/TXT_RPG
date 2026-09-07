using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TxTRPG.UI.Tests
{
    public sealed class HealthBarPanelPlayModeTests
    {
        [UnityTest]
        public IEnumerator DamagePulse_IsCancelledAndRestoredWhenEffectsAreDisabled()
        {
            var root = new GameObject("Health", typeof(RectTransform), typeof(HealthBarPanel));
            var visualObject = new GameObject("BarVisualRoot", typeof(RectTransform));
            visualObject.transform.SetParent(root.transform, false);
            var visual = (RectTransform)visualObject.transform;
            var panel = root.GetComponent<HealthBarPanel>();
            panel.ConfigureVisualRoots(null, visual, null, null, null);
            var pulse = root.AddComponent<HealthBarScalePulseEffect>();
            pulse.Configure(visual, 1.2f, 1f);
            panel.RegisterEffect(pulse);

            try
            {
                panel.Apply(Presentation(100));
                panel.Apply(Presentation(50));
                yield return null;
                Assert.That(visual.localScale.x, Is.GreaterThan(1f));

                panel.SetEffectsEnabled(false);
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                yield return null;
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator RootResize_ReappliesStretchLayoutWithoutTouchingVisualScale()
        {
            var root = new GameObject("Health", typeof(RectTransform), typeof(HealthBarLayoutController));
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(200f, 100f);
            rootRect.pivot = new Vector2(0.2f, 0.8f);
            var barObject = new GameObject("BarRoot", typeof(RectTransform));
            barObject.transform.SetParent(root.transform, false);
            var visualObject = new GameObject("BarVisualRoot", typeof(RectTransform));
            visualObject.transform.SetParent(barObject.transform, false);
            var visual = (RectTransform)visualObject.transform;
            visual.localScale = Vector3.one * 1.15f;
            var layout = root.GetComponent<HealthBarLayoutController>();
            layout.Configure(rootRect, (RectTransform)barObject.transform,
                HealthBarHorizontalAlignment.Center, HealthBarVerticalAlignment.Middle,
                HealthBarAxisSizeMode.Stretch, HealthBarAxisSizeMode.Stretch,
                new Vector2(100f, 24f), new RectOffset(12, 12, 12, 12), Vector2.zero);

            try
            {
                rootRect.sizeDelta = new Vector2(300f, 100f);
                yield return null;
                Assert.That(layout.BarRoot.rect.width, Is.EqualTo(276f).Within(0.01f));
                Assert.That(layout.BarRoot.rect.height, Is.EqualTo(76f).Within(0.01f));
                Assert.That(layout.BarRoot.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 1.15f));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static CharacterStatusPresentation Presentation(int current) =>
            new(new CharacterNamePresentation(string.Empty),
                new HealthPresentation(current, 100, "HP", $"{current} / 100"));
    }
}
