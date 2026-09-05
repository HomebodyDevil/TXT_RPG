using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class StoryTextPanelEditModePreviewTests
    {
        [Test]
        public void RebuiltDemoPrefab_ContainsOnePreviewItemPerDemoEntry()
        {
            StoryTextPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(
                "Assets/TxTRPG/UI/Demo/StoryTextPanelDemoData.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Demo/StoryTextPanelDemo.prefab");
            var previewItems = prefab.GetComponentsInChildren<StoryTextPanelDemoPreviewItem>(true);

            Assert.That(previewItems.Length, Is.EqualTo(data.Entries.Count));
        }
    }
}
