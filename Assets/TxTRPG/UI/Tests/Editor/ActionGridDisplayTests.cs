using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ActionGridDisplayTests
    {
        private static ActionGridLayoutResult Compute(ActionGridDisplayPreset p, float width, int count = 7, bool distribute = false)
        {
            p.Normalize();
            return ActionGridLayoutCalculator.Calculate(new Vector2(width, 200), count, ActionGridLayoutMode.FixedColumns,
                p.maximumColumns, p.minimumCellSize, p.maximumCellSize, p.spacing, p.padding,
                distribute, p.targetCellSize, p.minimumHorizontalGap, p.maximumHorizontalGap);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(19)]
        public void Balanced_PreservesCapacityIndependentColumnsAndHeight(int count)
        {
            var p = new ActionGridDisplayPreset(); var r = Compute(p, 600, count);
            Assert.That(r.Columns, Is.EqualTo(6)); Assert.That(r.CellSize, Is.EqualTo(Vector2.one * 72));
            Assert.That(r.Spacing, Is.EqualTo(Vector2.one * 16));
            Assert.That(r.RequiredHeight, Is.EqualTo(ActionGridPanel.CalculateRequiredGridHeight(count,6,72,16,p.padding)));
        }
        [TestCase(50,1,12)] [TestCase(168,2,12)] [TestCase(188,2,32)] [TestCase(1000,6,32)]
        public void Distributed_UsesBoundedGapAndFixedTarget(float width, int columns, float gap)
        {
            var r = Compute(new ActionGridDisplayPreset(), width, 7, true);
            Assert.That(r.Columns, Is.EqualTo(columns)); Assert.That(r.CellSize, Is.EqualTo(Vector2.one*64));
            Assert.That(r.Spacing.x, Is.EqualTo(gap)); Assert.That(r.Spacing.y, Is.EqualTo(16));
        }
        [Test]
        public void InvalidValues_AreFiniteAndSpaceShortageIsReported()
        {
            var p = new ActionGridDisplayPreset { maximumColumns=-5, minimumCellSize=new Vector2(float.NaN,-1), maximumCellSize=Vector2.zero,
                spacing = new Vector2(float.PositiveInfinity,-4), padding = new RectOffset(-1,-1,-1,-1), minimumHorizontalGap=90, maximumHorizontalGap=10 };
            p.Normalize(); var r = Compute(p,0);
            Assert.That(r.Columns, Is.EqualTo(1)); Assert.That(float.IsNaN(r.RequiredHeight), Is.False);
            Assert.That(float.IsInfinity(r.RequiredHeight), Is.False); Assert.That(r.IsReady, Is.False);
            Assert.That(r.HasInsufficientSpace, Is.True); Assert.That(p.maximumHorizontalGap, Is.EqualTo(90));
        }
        [Test]
        public void ExactColumns_RemainsExactUnderInsufficientWidth()
        {
            var r = ActionGridLayoutCalculator.Calculate(new Vector2(30,100), 8, ActionGridLayoutMode.ExactColumns,4,
                Vector2.one*72,Vector2.one*128,Vector2.one*8,new RectOffset());
            Assert.That(r.Columns,Is.EqualTo(4)); Assert.That(r.CellSize.x,Is.EqualTo(72)); Assert.That(r.HasInsufficientSpace,Is.True);
        }
        [Test]
        public void ModeSwitch_PreservesManualSettingsDataSelectionAndNavigation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab");
            var go = Object.Instantiate(prefab);
            try
            {
                var panel=go.GetComponent<ActionGridPanel>(); var viewport=panel.ScrollRect.viewport;
                viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,320);
                viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,200);
                panel.ConfigureLayout(ActionGridLayoutMode.FixedColumns,4,Vector2.one*42,Vector2.one*9,new RectOffset(3,3,3,3),ActionGridHorizontalAlignment.Left,ActionGridHorizontalAlignment.Right,ActionGridVerticalPlacement.Top);
                var settings=new ActionGridDisplaySettings { mode=ActionGridDisplayMode.Balanced };
                settings.balanced.spacing=new Vector2(17,19); panel.ConfigureDisplay(settings); settings.balanced.spacing=Vector2.zero;
                panel.SetEntries(System.Array.Empty<ActionGridEntry>(), 12);
                var capacity=panel.Capacity;
                foreach(var mode in new[]{ActionGridDisplayMode.DistributedSpacing,ActionGridDisplayMode.LargeSlots,ActionGridDisplayMode.Balanced,ActionGridDisplayMode.Manual})
                { panel.SetDisplayMode(mode); Assert.That(panel.Capacity,Is.EqualTo(capacity)); }
                Assert.That(panel.MinimumCellSize,Is.EqualTo(Vector2.one*42)); Assert.That(panel.Spacing,Is.EqualTo(Vector2.one*9));
                Assert.That(panel.GetDisplaySettings().balanced.spacing,Is.EqualTo(new Vector2(17,19)));
                go.SetActive(false); panel.SetDisplayMode(ActionGridDisplayMode.Balanced); go.SetActive(true);
                var cells=panel.ScrollRect.content.GetComponentsInChildren<ActionGridCell>();
                Assert.That(cells[0].Button.navigation.selectOnDown,Is.EqualTo(cells[panel.CurrentColumns].Button));
                panel.ConfigureLayout(ActionGridLayoutMode.ExactColumns,2,Vector2.one*44,Vector2.one,new RectOffset(),ActionGridHorizontalAlignment.Center,ActionGridHorizontalAlignment.Left,ActionGridVerticalPlacement.Top);
                Assert.That(panel.DisplayMode,Is.EqualTo(ActionGridDisplayMode.Manual));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void Settings_RoundTripAndCloneRetainIndependentModes()
        {
            var source=new ActionGridDisplaySettings(); source.Normalize(); source.balanced.spacing=Vector2.one*21; source.largeSlots.spacing=Vector2.one*25;
            var restored=JsonUtility.FromJson<ActionGridDisplaySettings>(JsonUtility.ToJson(source));
            var copy=restored.Copy(); copy.balanced.padding.left=80;
            Assert.That(restored.balanced.padding.left,Is.EqualTo(14));
            Assert.That(restored.largeSlots.spacing.x,Is.EqualTo(25)); Assert.That(restored.mode,Is.EqualTo(ActionGridDisplayMode.Manual));
        }
    }
}
