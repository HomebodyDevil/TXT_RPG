using System.Collections.Generic;
using NUnit.Framework;
using TxTRPG.UI.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class EnemyDisplayPanelTests
    {
        [Test]
        public void Presentation_UsesInstanceIdentitySeparatelyFromEnemyType()
        {
            var first = new EnemyPresentation("slime-a", "slime", formationIndex: -1);
            var second = new EnemyPresentation("slime-b", "slime");

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
            Assert.That(first.EnemyId, Is.EqualTo(second.EnemyId));
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(first.FormationIndex, Is.Zero);
        }

        [TestCase(1, 1)]
        [TestCase(3, 3)]
        [TestCase(5, 5)]
        public void ResponsiveFormation_CentersEveryRowAndRespectsBounds(int count, int expected)
        {
            var placements = new List<EnemyPlacement>();
            ResponsiveHorizontalEnemyLayoutStrategy.Calculate(
                count,
                new Rect(-380f, -260f, 760f, 520f),
                3,
                new Vector2(180f, 260f),
                24f,
                12f,
                0.45f,
                1f,
                Vector2.zero,
                placements);

            Assert.That(placements, Has.Count.EqualTo(expected));
            Assert.That(placements[0].Scale, Is.InRange(0.45f, 1f));
            if (count == 1)
            {
                Assert.That(placements[0].AnchoredPosition, Is.EqualTo(Vector2.zero));
            }
            if (count == 5)
            {
                Assert.That(
                    placements[3].AnchoredPosition.x + placements[4].AnchoredPosition.x,
                    Is.EqualTo(0f).Within(0.001f));
            }
        }

        [Test]
        public void GeneratedPrefab_HasIndependentPanelPooledBackendAndSafeImages()
        {
            EnemyDisplayPanelPrefabBuilder.CreateOrUpdatePrefab();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyDisplayPanelPrefabBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var panel = prefab.GetComponent<EnemyDisplayPanel>();
            var backend = prefab.GetComponentInChildren<Enemy2DDisplayBackend>(true);
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.BackgroundRenderer, Is.Not.Null);
            Assert.That(backend, Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterDisplayPanel>(), Is.Null);
            Assert.That(prefab.GetComponentInChildren<ResponsiveHorizontalEnemyLayoutStrategy>(true),
                Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "EnemyDisplayRoot/Enemy2DBackend/PoolRoot/Enemy2DViewTemplate"), Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "EnemyDisplayRoot/Enemy2DBackend/PoolRoot/Enemy2DViewTemplate/FrameViewport/VisualRoot/ArtworkRoot/BaseImage"),
                Is.Not.Null);
            Assert.That(prefab.transform.Find(
                "EnemyDisplayRoot/Enemy2DBackend/PoolRoot/Enemy2DViewTemplate/TargetMarker"), Is.Not.Null);
            foreach (var image in prefab.GetComponentsInChildren<Image>(true))
            {
                Assert.That(image.raycastTarget, Is.False, image.name);
            }
        }

        [Test]
        public void Backend_PreservesViewsByInstanceIdAndReusesReleasedView()
        {
            EnemyDisplayPanelPrefabBuilder.CreateOrUpdatePrefab();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyDisplayPanelPrefabBuilder.PrefabPath);
            var instance = Object.Instantiate(prefab);
            try
            {
                var panel = instance.GetComponent<EnemyDisplayPanel>();
                var backend = instance.GetComponentInChildren<Enemy2DDisplayBackend>(true);
                panel.SetEnemies(new[]
                {
                    new EnemyPresentation("slime-a", "slime", formationIndex: 0),
                    new EnemyPresentation("slime-b", "slime", formationIndex: 1, isTargeted: true),
                    new EnemyPresentation("slime-c", "slime", formationIndex: 2)
                });

                Assert.That(backend.ActiveViewCount, Is.EqualTo(3));
                Assert.That(backend.TryGetView("slime-a", out var firstView), Is.True);
                Assert.That(backend.TryGetView("slime-b", out var targetedView), Is.True);
                Assert.That(targetedView.IsTargeted, Is.True);

                panel.SetEnemies(new[]
                {
                    new EnemyPresentation("slime-b", "slime", formationIndex: 0),
                    new EnemyPresentation("slime-c", "slime", formationIndex: 1),
                    new EnemyPresentation("slime-d", "slime", formationIndex: 2)
                });

                Assert.That(backend.ActiveViewCount, Is.EqualTo(3));
                Assert.That(backend.TryGetView("slime-d", out var reusedView), Is.True);
                Assert.That(reusedView, Is.SameAs(firstView));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void GeneratedDemo_HasThreeVisibleEnemiesAndStartupLoader()
        {
            EnemyDisplayPanelPrefabBuilder.CreateOrUpdatePrefab();
            EnemyDisplayPanelDemoBuilder.CreateOrUpdateDemo();

            var data = AssetDatabase.LoadAssetAtPath<EnemyDisplayPanelDemoData>(
                "Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayPanelDemoData.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyDisplayPanelDemoBuilder.DemoPrefabPath);
            Assert.That(data, Is.Not.Null);
            Assert.That(data.EnemyCount, Is.EqualTo(3));
            Assert.That(data.AppearanceDefinition, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<EnemyDisplayPanelDemoLoader>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PanelStartupController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<FadePanelRevealTransition>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Enemy2DView>(false), Has.Length.EqualTo(3));

            var presentations = data.CreatePresentations();
            Assert.That(data.AppearanceDefinition.TryResolveReference(
                presentations[0], out var artwork), Is.True);
            Assert.That(artwork.AssetId, Is.Not.Empty);
        }
    }
}
