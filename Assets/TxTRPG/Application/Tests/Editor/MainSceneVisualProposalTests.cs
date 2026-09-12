using System.Linq;
using NUnit.Framework;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Presentation;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Tests
{
    public sealed class MainSceneVisualProposalTests
    {
        [Test]
        public void Builder_CreatesIsolatedResponsiveProposal()
        {
            var sourceGuid = AssetDatabase.AssetPathToGUID(MainSceneVisualProposalBuilder.SourceScenePath);
            MainSceneVisualProposalBuilder.CreateOrUpdate();

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(MainSceneVisualProposalBuilder.ProposalScenePath), Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(MainSceneVisualProposalBuilder.ProposalScenePath), Is.Not.EqualTo(sourceGuid));
            Assert.That(AssetDatabase.LoadAssetAtPath<FlexibleLayoutBackgroundStyle>(MainSceneVisualProposalBuilder.MainStylePath), Is.Not.Null);

            var scene = EditorSceneManager.OpenScene(MainSceneVisualProposalBuilder.ProposalScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var marker = roots.SelectMany(root => root.GetComponentsInChildren<MainSceneVisualProposalMarker>(true)).Single();
                var main = marker.GetComponent<FlexibleLayoutPanel>();
                var placeholder = roots.SelectMany(root => root.GetComponentsInChildren<FlexibleLayoutPanel>(true))
                    .Single(panel => panel.name == "TMP_FlexibleLayoutPanel");
                var controller = marker.GetComponent<MainScenePresentationController>();

                Assert.That(marker.SourceScenePath, Is.EqualTo(MainSceneVisualProposalBuilder.SourceScenePath));
                Assert.That(main.ResolveAxis(1920f), Is.EqualTo(FlexibleLayoutAxis.Horizontal));
                Assert.That(main.ResolveAxis(720f), Is.EqualTo(FlexibleLayoutAxis.Vertical));
                Assert.That(placeholder.gameObject.activeSelf, Is.False);
                Assert.That(controller.IsPreviewMode, Is.True);
                Assert.That(main.transform.Find("BackgroundLayer/BackgroundVisualRoot/VisualProposalStoryGlow"), Is.Not.Null);
                Assert.That(main.transform.Find("BackgroundLayer/BackgroundVisualRoot/VisualProposalCharacterWash"), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Proposal_IsEnabledInBuildSettingsWithoutChangingInitialSceneOrder()
        {
            MainSceneVisualProposalBuilder.CreateOrUpdate();
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes[0].path, Is.EqualTo("Assets/Scenes/AppScene.unity"));
            Assert.That(scenes.Any(scene => scene.enabled && scene.path == MainSceneVisualProposalBuilder.ProposalScenePath), Is.True);
        }
    }
}
