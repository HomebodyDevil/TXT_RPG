using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

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
        public void GeneratedPanelPrefab_HasRequiredReferences()
        {
            StoryTextPanelPrefabBuilder.CreateOrUpdatePrefabs();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<StoryTextPanel>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<UnityEngine.UI.ScrollRect>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<UnityEngine.UI.Scrollbar>(true), Is.Not.Null);
        }
    }
}
