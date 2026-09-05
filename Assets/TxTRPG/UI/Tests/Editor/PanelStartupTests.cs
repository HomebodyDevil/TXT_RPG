using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.UI.Tests
{
    public sealed class PanelStartupTestLoader : PanelInitialDataLoader
    {
        public override bool HasInitialData => true;
        public bool Applied { get; private set; }
        public override Task<PanelLoadResult> LoadAndApplyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Applied = true;
            return Task.FromResult(PanelLoadResult.Success);
        }
    }

    public sealed class PanelStartupTests
    {
        [Test]
        public async Task InitializeAsync_BlocksInputUntilDataAndRevealAreComplete()
        {
            var root = new GameObject("StartupPanel", typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var group = root.AddComponent<CanvasGroup>();
                var loader = root.AddComponent<PanelStartupTestLoader>();
                var transition = root.AddComponent<FadePanelRevealTransition>();
                var transitionProperties = new SerializedObject(transition);
                transitionProperties.FindProperty("animateReveal").boolValue = false;
                transitionProperties.FindProperty("duration").floatValue = 0f;
                transitionProperties.ApplyModifiedPropertiesWithoutUndo();
                var controller = root.AddComponent<PanelStartupController>();
                var properties = new SerializedObject(controller);
                properties.FindProperty("canvasGroup").objectReferenceValue = group;
                properties.FindProperty("initialDataLoader").objectReferenceValue = loader;
                properties.FindProperty("revealTransition").objectReferenceValue = transition;
                properties.FindProperty("layoutRoot").objectReferenceValue = root.transform;
                properties.FindProperty("autoStart").boolValue = false;
                properties.ApplyModifiedPropertiesWithoutUndo();

                root.SetActive(true);
                var initialization = controller.InitializeAsync();
                Assert.That(group.alpha, Is.Zero);
                Assert.That(group.interactable, Is.False);
                Assert.That(group.blocksRaycasts, Is.False);
                await initialization;
                Assert.That(loader.Applied, Is.True);
                Assert.That(controller.State, Is.EqualTo(PanelStartupState.Ready));
                Assert.That(group.alpha, Is.EqualTo(1f));
                Assert.That(group.interactable, Is.True);
                Assert.That(group.blocksRaycasts, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public async Task RevealAnimation_DisabledByDefault_CompletesImmediatelyAfterPreparation()
        {
            var root = new GameObject("Reveal", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var group = root.GetComponent<CanvasGroup>();
                var transition = root.AddComponent<FadePanelRevealTransition>();
                transition.PrepareHidden(group);

                Assert.That(group.alpha, Is.Zero);
                await transition.RevealAsync(group, CancellationToken.None);
                Assert.That(group.alpha, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("Assets/TxTRPG/UI/Demo/StoryTextPanelDemo.prefab")]
        [TestCase("Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayPanelDemo.prefab")]
        [TestCase("Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemo.prefab")]
        [TestCase("Assets/TxTRPG/UI/DEMO/ActionGridPanel/ActionGridPanelDemo.prefab")]
        public void DemoPrefab_LocalRevealAnimationIsDisabled(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var transition = prefab.GetComponent<FadePanelRevealTransition>();
            Assert.That(transition, Is.Not.Null, prefabPath);
            var properties = new SerializedObject(transition);
            Assert.That(properties.FindProperty("animateReveal").boolValue, Is.False, prefabPath);
        }
    }
}
