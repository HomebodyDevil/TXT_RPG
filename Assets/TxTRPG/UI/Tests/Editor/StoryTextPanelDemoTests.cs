using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class StoryTextPanelDemoTests
    {
        [Test]
        public void GeneratedDemo_ContainsMixedMessagesAndConnectedLoader()
        {
            StoryTextPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<StoryTextPanelDemoData>(
                "Assets/TxTRPG/UI/Demo/StoryTextPanelDemoData.asset");
            Assert.That(data, Is.Not.Null);
            Assert.That(data.Entries.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(data.Entries, Has.Some.Matches<StoryTextPanelDemoData.Entry>(
                entry => entry.ToMessage().HasSpeaker));
            Assert.That(data.Entries, Has.Some.Matches<StoryTextPanelDemoData.Entry>(
                entry => !entry.ToMessage().HasSpeaker));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/TxTRPG/UI/Demo/StoryTextPanelDemo.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<StoryTextPanelDemoLoader>(), Is.Not.Null);
            var panel = prefab.GetComponentInChildren<StoryTextPanel>(true);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.BackgroundRenderer, Is.Not.Null);
            Assert.That(panel.BackgroundRenderer.InitialStyle, Is.Not.Null);
        }
    }
}
