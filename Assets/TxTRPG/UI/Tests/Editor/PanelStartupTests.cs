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
    }
}
